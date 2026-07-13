use chrono::{DateTime, Utc};
use sqlx::PgPool;

pub struct SignatureCloud {
    pool: PgPool,
}

#[derive(serde::Serialize, sqlx::FromRow)]
pub struct SignatureEntry {
    pub signature: String,
    pub operator_token: String,
    pub created_at: DateTime<Utc>,
}

impl SignatureCloud {
    pub async fn new(pool: PgPool) -> Result<Self, sqlx::Error> {
        sqlx::query(
            "CREATE TABLE IF NOT EXISTS signature_cloud (
                signature TEXT PRIMARY KEY,
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
        signature: &str,
        operator_token: &str,
    ) -> Result<(), sqlx::Error> {
        sqlx::query(
            "INSERT INTO signature_cloud (signature, label, operator_token)
             VALUES ($1, 0, $2)
             ON CONFLICT (signature) DO UPDATE
             SET label = 0, operator_token = EXCLUDED.operator_token",
        )
        .bind(signature)
        .bind(operator_token)
        .execute(&self.pool)
        .await?;
        Ok(())
    }

    pub async fn delete(&self, signature: &str) -> Result<u64, sqlx::Error> {
        let result = sqlx::query("DELETE FROM signature_cloud WHERE signature = $1")
            .bind(signature)
            .execute(&self.pool)
            .await?;
        Ok(result.rows_affected())
    }

    pub async fn get(&self, signature: &str) -> Result<Option<SignatureEntry>, sqlx::Error> {
        let row = sqlx::query_as::<_, SignatureEntry>(
            "SELECT signature, operator_token, created_at FROM signature_cloud WHERE signature = $1",
        )
        .bind(signature)
        .fetch_optional(&self.pool)
        .await?;
        Ok(row)
    }

    pub async fn list(
        &self,
        limit: i64,
        offset: i64,
    ) -> Result<Vec<SignatureEntry>, sqlx::Error> {
        let rows = sqlx::query_as::<_, SignatureEntry>(
            "SELECT signature, operator_token, created_at
             FROM signature_cloud
             ORDER BY created_at DESC
             LIMIT $1 OFFSET $2",
        )
        .bind(limit)
        .bind(offset)
        .fetch_all(&self.pool)
        .await?;
        Ok(rows)
    }

    pub async fn clear(&self) -> Result<u64, sqlx::Error> {
        let result = sqlx::query("DELETE FROM signature_cloud")
            .execute(&self.pool)
            .await?;
        Ok(result.rows_affected())
    }
}
