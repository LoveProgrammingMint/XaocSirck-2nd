use chrono::{DateTime, Utc};
use sqlx::PgPool;

pub struct Community {
    pool: PgPool,
}

#[derive(serde::Serialize, sqlx::FromRow)]
pub struct ReportEntry {
    pub sha256: String,
    pub path: String,
    pub created_at: DateTime<Utc>,
}

impl Community {
    pub async fn new(pool: PgPool) -> Result<Self, sqlx::Error> {
        sqlx::query(
            "CREATE TABLE IF NOT EXISTS community_reports (
                sha256 TEXT PRIMARY KEY,
                path TEXT NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            )",
        )
        .execute(&pool)
        .await?;
        Ok(Self { pool })
    }

    pub async fn report(&self, sha256: &str, path: &str) -> Result<(), sqlx::Error> {
        sqlx::query(
            "INSERT INTO community_reports (sha256, path)
             VALUES ($1, $2)
             ON CONFLICT (sha256) DO UPDATE
             SET path = EXCLUDED.path",
        )
        .bind(sha256)
        .bind(path)
        .execute(&self.pool)
        .await?;
        Ok(())
    }

    pub async fn list(&self) -> Result<Vec<ReportEntry>, sqlx::Error> {
        sqlx::query_as::<_, ReportEntry>(
            "SELECT sha256, path, created_at FROM community_reports ORDER BY created_at DESC",
        )
        .fetch_all(&self.pool)
        .await
    }

    pub async fn delete(&self, sha256: &str) -> Result<u64, sqlx::Error> {
        let result = sqlx::query("DELETE FROM community_reports WHERE sha256 = $1")
            .bind(sha256)
            .execute(&self.pool)
            .await?;
        Ok(result.rows_affected())
    }
}
