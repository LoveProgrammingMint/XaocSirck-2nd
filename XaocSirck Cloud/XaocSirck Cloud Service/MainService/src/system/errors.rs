use axum::{
    extract::{Query, State},
    http::StatusCode,
    Json,
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::{FromRow, QueryBuilder};
use std::sync::Arc;

use crate::system::SystemState;

#[derive(Deserialize)]
pub struct ErrorQuery {
    pub subsystem: Option<String>,
    pub level: Option<String>,
    pub limit: Option<i64>,
    pub offset: Option<i64>,
}

#[derive(Serialize, FromRow)]
pub struct ErrorLog {
    pub id: i64,
    pub time: DateTime<Utc>,
    pub subsystem: String,
    pub level: String,
    pub message: String,
    pub callback: Option<String>,
    pub forwarded: bool,
}

#[derive(Deserialize)]
pub struct ErrorCreate {
    pub subsystem: String,
    pub level: String,
    pub message: String,
    pub callback: Option<String>,
}

pub async fn list(
    State(state): State<Arc<SystemState>>,
    Query(q): Query<ErrorQuery>,
) -> Result<Json<Vec<ErrorLog>>, StatusCode> {
    let limit = q.limit.unwrap_or(50);
    let offset = q.offset.unwrap_or(0);

    let mut builder: QueryBuilder<sqlx::Postgres> = QueryBuilder::new(
        "SELECT id, time, subsystem, level, message, callback, forwarded FROM error_logs WHERE 1=1"
    );
    if let Some(subsystem) = q.subsystem {
        builder.push(" AND subsystem = ").push_bind(subsystem);
    }
    if let Some(level) = q.level {
        builder.push(" AND level = ").push_bind(level);
    }
    builder
        .push(" ORDER BY time DESC LIMIT ")
        .push_bind(limit)
        .push(" OFFSET ")
        .push_bind(offset);

    let logs = builder
        .build_query_as::<ErrorLog>()
        .fetch_all(&state.pool)
        .await
        .map_err(|e| {
            eprintln!("error list failed: {}", e);
            StatusCode::INTERNAL_SERVER_ERROR
        })?;

    Ok(Json(logs))
}

pub async fn create(
    State(state): State<Arc<SystemState>>,
    Json(body): Json<ErrorCreate>,
) -> Result<StatusCode, StatusCode> {
    sqlx::query(
        "INSERT INTO error_logs (subsystem, level, message, callback) VALUES ($1, $2, $3, $4)",
    )
    .bind(&body.subsystem)
    .bind(&body.level)
    .bind(&body.message)
    .bind(body.callback.as_deref().unwrap_or(""))
    .execute(&state.pool)
    .await
    .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(StatusCode::CREATED)
}
