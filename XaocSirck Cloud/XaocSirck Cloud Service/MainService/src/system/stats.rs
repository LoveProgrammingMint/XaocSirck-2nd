use axum::{
    body::Body,
    extract::{ConnectInfo, Extension, Query, State},
    http::{Request, StatusCode},
    middleware::Next,
    response::{IntoResponse, Response},
    Json,
};
use serde::Deserialize;
use sqlx::Row;
use std::net::SocketAddr;
use std::sync::Arc;
use std::time::Instant;

use crate::system::SystemState;

pub async fn log_request(
    Extension(state): Extension<Arc<SystemState>>,
    ConnectInfo(addr): ConnectInfo<SocketAddr>,
    req: Request<Body>,
    next: Next,
) -> Response {
    let ip = addr.ip().to_string();
    let method = req.method().to_string();
    let path = req.uri().path().to_string();
    let start = Instant::now();

    let response = next.run(req).await;

    let status = response.status().as_u16() as i16;
    let duration = start.elapsed().as_millis() as i32;
    let size = response
        .headers()
        .get("content-length")
        .and_then(|v| v.to_str().ok().and_then(|s| s.parse::<i64>().ok()))
        .unwrap_or(0);

    let pool = state.pool.clone();
    let ip2 = ip.clone();
    tokio::spawn(async move {
        let _ = sqlx::query(
            "INSERT INTO request_logs (ip, method, path, status_code, duration_ms, response_size)
             VALUES ($1::INET, $2, $3, $4, $5, $6)",
        )
        .bind(ip2)
        .bind(method)
        .bind(path)
        .bind(status)
        .bind(duration)
        .bind(size)
        .execute(&pool)
        .await;
    });

    if status >= 500 {
        let pool = state.pool.clone();
        tokio::spawn(async move {
            let _ = sqlx::query(
                "INSERT INTO error_logs (subsystem, level, message, forwarded)
                 VALUES ('system-host', 'error', $1, FALSE)",
            )
            .bind(format!("HTTP {} from {}", status, ip))
            .execute(&pool)
            .await;
        });
    }

    response
}

pub async fn check_blacklist(
    Extension(state): Extension<Arc<SystemState>>,
    ConnectInfo(addr): ConnectInfo<SocketAddr>,
    req: Request<Body>,
    next: Next,
) -> Response {
    let ip = addr.ip().to_string();
    let blocked: Option<(String,)> = sqlx::query_as(
        "SELECT reason FROM ip_blacklist WHERE ip = $1::INET"
    )
    .bind(&ip)
    .fetch_optional(&state.pool)
    .await
    .unwrap_or(None);

    if blocked.is_some() {
        return (StatusCode::FORBIDDEN, "IP blocked").into_response();
    }

    next.run(req).await
}

#[derive(Deserialize)]
pub struct RangeQuery {
    pub range: Option<String>,
}

#[derive(serde::Serialize)]
pub struct StatsResponse {
    pub total_requests: i64,
    pub requests_1h: i64,
    pub requests_1d: i64,
    pub avg_duration_ms: f64,
    pub peak_duration_ms: i32,
    pub avg_response_size: f64,
}

pub async fn stats(State(state): State<Arc<SystemState>>) -> Result<Json<StatsResponse>, StatusCode> {
    let pool = &state.pool;
    let total: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM request_logs")
        .fetch_one(pool)
        .await
        .map_err(|e| {
            eprintln!("stats total failed: {}", e);
            StatusCode::INTERNAL_SERVER_ERROR
        })?;
    let requests_1h: i64 = sqlx::query_scalar(
        "SELECT COUNT(*) FROM request_logs WHERE time > NOW() - INTERVAL '1 hour'"
    )
    .fetch_one(pool)
    .await
    .map_err(|e| {
        eprintln!("stats requests_1h failed: {}", e);
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let requests_1d: i64 = sqlx::query_scalar(
        "SELECT COUNT(*) FROM request_logs WHERE time > NOW() - INTERVAL '1 day'"
    )
    .fetch_one(pool)
    .await
    .map_err(|e| {
        eprintln!("stats requests_1d failed: {}", e);
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let avg_duration_ms: f64 = sqlx::query_scalar(
        "SELECT COALESCE(AVG(duration_ms), 0) FROM request_logs WHERE time > NOW() - INTERVAL '1 hour'"
    )
    .fetch_one(pool)
    .await
    .map_err(|e| {
        eprintln!("stats avg_duration_ms failed: {}", e);
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let peak_duration_ms: i32 = sqlx::query_scalar(
        "SELECT COALESCE(MAX(duration_ms), 0) FROM request_logs WHERE time > NOW() - INTERVAL '1 hour'"
    )
    .fetch_one(pool)
    .await
    .map_err(|e| {
        eprintln!("stats peak_duration_ms failed: {}", e);
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let avg_response_size: f64 = sqlx::query_scalar(
        "SELECT COALESCE(AVG(response_size), 0) FROM request_logs WHERE time > NOW() - INTERVAL '1 hour'"
    )
    .fetch_one(pool)
    .await
    .map_err(|e| {
        eprintln!("stats avg_response_size failed: {}", e);
        StatusCode::INTERNAL_SERVER_ERROR
    })?;

    Ok(Json(StatsResponse {
        total_requests: total,
        requests_1h,
        requests_1d,
        avg_duration_ms,
        peak_duration_ms,
        avg_response_size,
    }))
}

#[derive(serde::Serialize)]
pub struct HistogramResponse {
    pub labels: Vec<String>,
    pub values: Vec<i64>,
}

pub async fn histogram(
    State(state): State<Arc<SystemState>>,
    Query(q): Query<RangeQuery>,
) -> Result<Json<HistogramResponse>, StatusCode> {
    let range = q.range.as_deref().unwrap_or("1h");
    let (interval_minutes, buckets): (i32, i32) = match range {
        "1d" => (60, 24),
        _ => (10, 6),
    };

    let rows = sqlx::query(
        "SELECT bucket, COALESCE(cnt, 0) as cnt FROM (
            SELECT date_trunc('hour', time) + INTERVAL '1 minute' * (extract(minute FROM time)::int / $1 * $1) AS bucket,
                   COUNT(*) AS cnt
            FROM request_logs
            WHERE time > NOW() - (INTERVAL '1 minute' * $1 * $2)
            GROUP BY bucket
            ORDER BY bucket
        ) t",
    )
    .bind(interval_minutes)
    .bind(buckets)
    .fetch_all(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    let mut values = vec![0i64; buckets as usize];
    let mut labels = Vec::with_capacity(buckets as usize);

    for (i, row) in rows.iter().enumerate() {
        if i < values.len() {
            values[i] = row.get("cnt");
        }
    }

    let now = chrono::Utc::now();
    for i in (0..buckets).rev() {
        let t = now - chrono::Duration::minutes((interval_minutes * (i + 1)).into());
        labels.insert(0, t.format("%H:%M").to_string());
    }

    Ok(Json(HistogramResponse { labels, values }))
}

