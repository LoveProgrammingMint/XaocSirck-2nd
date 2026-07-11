use chrono::{DateTime, Utc};
use sqlx::{PgPool, Row};

pub struct ColdCache {
    pool: PgPool,
}

#[derive(serde::Serialize)]
pub struct ColdEntry {
    pub sha256: String,
    pub label: i16,
    pub operator_token: String,
    pub created_at: DateTime<Utc>,
}

impl ColdCache {
    pub async fn new(pool: PgPool) -> Result<Self, sqlx::Error> {
        sqlx::query(
            "CREATE TABLE IF NOT EXISTS cold_cache (
                sha256 BYTEA PRIMARY KEY,
                label SMALLINT NOT NULL,
                operator_token TEXT NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            )",
        )
        .execute(&pool)
        .await?;
        Ok(Self { pool })
    }

    pub async fn add(
        &self,
        sha256: &[u8],
        label: i16,
        operator_token: &str,
    ) -> Result<(), sqlx::Error> {
        sqlx::query(
            "INSERT INTO cold_cache (sha256, label, operator_token)
             VALUES ($1, $2, $3)
             ON CONFLICT (sha256) DO UPDATE
             SET label = EXCLUDED.label, operator_token = EXCLUDED.operator_token",
        )
        .bind(sha256)
        .bind(label)
        .bind(operator_token)
        .execute(&self.pool)
        .await?;
        Ok(())
    }

    pub async fn delete(&self, sha256: &[u8]) -> Result<u64, sqlx::Error> {
        let result = sqlx::query("DELETE FROM cold_cache WHERE sha256 = $1")
            .bind(sha256)
            .execute(&self.pool)
            .await?;
        Ok(result.rows_affected())
    }

    pub async fn get(&self, sha256: &[u8]) -> Result<Option<ColdEntry>, sqlx::Error> {
        let row =
            sqlx::query("SELECT sha256, label, operator_token, created_at FROM cold_cache WHERE sha256 = $1")
                .bind(sha256)
                .fetch_optional(&self.pool)
                .await?;
        Ok(row.map(|r| ColdEntry {
            sha256: hex::encode(r.get::<Vec<u8>, _>("sha256")),
            label: r.get("label"),
            operator_token: r.get("operator_token"),
            created_at: r.get("created_at"),
        }))
    }

    pub async fn list(&self, limit: i64, offset: i64) -> Result<Vec<ColdEntry>, sqlx::Error> {
        let rows = sqlx::query(
            "SELECT sha256, label, operator_token, created_at FROM cold_cache ORDER BY created_at DESC LIMIT $1 OFFSET $2",
        )
        .bind(limit)
        .bind(offset)
        .fetch_all(&self.pool)
        .await?;
        Ok(rows
            .into_iter()
            .map(|r| ColdEntry {
                sha256: hex::encode(r.get::<Vec<u8>, _>("sha256")),
                label: r.get("label"),
                operator_token: r.get("operator_token"),
                created_at: r.get("created_at"),
            })
            .collect())
    }

    pub async fn clear(&self) -> Result<u64, sqlx::Error> {
        let result = sqlx::query("DELETE FROM cold_cache")
            .execute(&self.pool)
            .await?;
        Ok(result.rows_affected())
    }
}
