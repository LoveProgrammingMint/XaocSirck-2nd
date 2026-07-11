pub mod errors;
pub mod health;
pub mod routes;
pub mod security;
pub mod stats;

use sqlx::PgPool;
use std::sync::Arc;

#[derive(Clone)]
pub struct SystemState {
    pub pool: PgPool,
}

pub async fn init(pool: PgPool) -> Result<Arc<SystemState>, sqlx::Error> {
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS request_logs (
            id BIGSERIAL PRIMARY KEY,
            time TIMESTAMPTZ DEFAULT NOW(),
            ip INET NOT NULL,
            method TEXT NOT NULL,
            path TEXT NOT NULL,
            status_code SMALLINT NOT NULL,
            duration_ms INTEGER NOT NULL,
            response_size BIGINT NOT NULL DEFAULT 0
        )",
    )
    .execute(&pool)
    .await?;
    sqlx::query(
        "CREATE INDEX IF NOT EXISTS idx_request_logs_time ON request_logs(time)"
    )
    .execute(&pool)
    .await?;
    sqlx::query(
        "CREATE INDEX IF NOT EXISTS idx_request_logs_ip ON request_logs(ip)"
    )
    .execute(&pool)
    .await?;

    sqlx::query(
        "CREATE TABLE IF NOT EXISTS error_logs (
            id BIGSERIAL PRIMARY KEY,
            time TIMESTAMPTZ DEFAULT NOW(),
            subsystem TEXT NOT NULL,
            level TEXT NOT NULL,
            message TEXT NOT NULL,
            callback TEXT,
            forwarded BOOLEAN NOT NULL DEFAULT FALSE
        )",
    )
    .execute(&pool)
    .await?;
    sqlx::query(
        "CREATE INDEX IF NOT EXISTS idx_error_logs_time ON error_logs(time)"
    )
    .execute(&pool)
    .await?;

    sqlx::query(
        "CREATE TABLE IF NOT EXISTS ip_blacklist (
            ip INET PRIMARY KEY,
            reason TEXT NOT NULL,
            created_at TIMESTAMPTZ DEFAULT NOW()
        )",
    )
    .execute(&pool)
    .await?;

    Ok(Arc::new(SystemState { pool }))
}
