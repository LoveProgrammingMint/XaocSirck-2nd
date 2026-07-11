use axum::{extract::State, http::StatusCode, Json};
use serde::Serialize;
use sqlx::Row;
use std::sync::Arc;

use crate::system::SystemState;

#[derive(Serialize)]
pub struct RouteStat {
    pub route: String,
    pub method: String,
    pub calls: i64,
    pub avg_ms: f64,
}

pub async fn route_stats(
    State(state): State<Arc<SystemState>>,
) -> Result<Json<Vec<RouteStat>>, StatusCode> {
    let rows = sqlx::query(
        "SELECT path, method, COUNT(*) as calls, AVG(duration_ms)::FLOAT8 as avg_ms
         FROM request_logs
         WHERE time > NOW() - INTERVAL '1 day'
         GROUP BY path, method
         ORDER BY calls DESC
         LIMIT 100",
    )
    .fetch_all(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    let mut stats = Vec::with_capacity(rows.len());
    for row in rows {
        stats.push(RouteStat {
            route: row.get("path"),
            method: row.get("method"),
            calls: row.get("calls"),
            avg_ms: row.get::<f64, _>("avg_ms"),
        });
    }
    Ok(Json(stats))
}
