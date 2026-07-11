use axum::{
    extract::{Query, State},
    http::StatusCode,
    Json,
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::{FromRow, Row};
use std::sync::Arc;

use crate::system::SystemState;

#[derive(Serialize, FromRow)]
pub struct BlacklistEntry {
    pub ip: String,
    pub reason: String,
    pub created_at: DateTime<Utc>,
}

#[derive(Deserialize)]
pub struct BlacklistCreate {
    pub ip: String,
    pub reason: String,
}

#[derive(Deserialize)]
pub struct IPQuery {
    pub limit: Option<i64>,
    pub offset: Option<i64>,
}

#[derive(Serialize)]
pub struct IPRecord {
    pub ip: String,
    pub requests: i64,
    pub last_access: Option<DateTime<Utc>>,
    pub status: String,
}

pub async fn list_blacklist(
    State(state): State<Arc<SystemState>>,
) -> Result<Json<Vec<BlacklistEntry>>, StatusCode> {
    let rows = sqlx::query_as::<_, BlacklistEntry>(
        "SELECT ip::TEXT as ip, reason, created_at FROM ip_blacklist ORDER BY created_at DESC"
    )
    .fetch_all(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(Json(rows))
}

pub async fn add_blacklist(
    State(state): State<Arc<SystemState>>,
    Json(body): Json<BlacklistCreate>,
) -> Result<StatusCode, StatusCode> {
    sqlx::query(
        "INSERT INTO ip_blacklist (ip, reason) VALUES ($1::INET, $2)
         ON CONFLICT (ip) DO UPDATE SET reason = EXCLUDED.reason"
    )
    .bind(&body.ip)
    .bind(&body.reason)
    .execute(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(StatusCode::CREATED)
}

pub async fn remove_blacklist(
    State(state): State<Arc<SystemState>>,
    Query(q): Query<std::collections::HashMap<String, String>>,
) -> Result<StatusCode, StatusCode> {
    let ip = q.get("ip").ok_or(StatusCode::BAD_REQUEST)?;
    sqlx::query("DELETE FROM ip_blacklist WHERE ip = $1::INET")
        .bind(ip)
        .execute(&state.pool)
        .await
        .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(StatusCode::NO_CONTENT)
}

pub async fn ip_stats(
    State(state): State<Arc<SystemState>>,
    Query(q): Query<IPQuery>,
) -> Result<Json<Vec<IPRecord>>, StatusCode> {
    let limit = q.limit.unwrap_or(50);
    let offset = q.offset.unwrap_or(0);

    let rows = sqlx::query(
        "SELECT ip::TEXT as ip, COUNT(*) as requests, MAX(time) as last_access
         FROM request_logs
         GROUP BY ip
         ORDER BY requests DESC
         LIMIT $1 OFFSET $2"
    )
    .bind(limit)
    .bind(offset)
    .fetch_all(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    let mut records = Vec::with_capacity(rows.len());
    for row in rows {
        let ip: String = row.get("ip");
        let blocked: Option<(String,)> = sqlx::query_as(
            "SELECT reason FROM ip_blacklist WHERE ip = $1::INET"
        )
        .bind(&ip)
        .fetch_optional(&state.pool)
        .await
        .unwrap_or(None);

        records.push(IPRecord {
            status: if blocked.is_some() { "blocked".to_string() } else { "normal".to_string() },
            ip,
            requests: row.get("requests"),
            last_access: row.get("last_access"),
        });
    }

    Ok(Json(records))
}
