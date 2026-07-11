mod auth;
mod cold;
mod control;
mod hot;
mod routes;

pub use cold::ColdCache;
pub use control::Control;
pub use hot::HotCache;
pub use routes::{router, AppState};

use common::Settings;
use sqlx::PgPool;
use std::sync::Arc;

pub async fn create_state(settings: &Settings) -> Arc<AppState> {
    let pool = PgPool::connect(&settings.database_url)
        .await
        .expect("database connect failed");
    create_state_with_pool(pool, settings.jwt_public_key.clone()).await
}

pub async fn create_state_with_pool(pool: PgPool, jwt_public_key: String) -> Arc<AppState> {
    let cold = ColdCache::new(pool)
        .await
        .expect("cold cache init failed");
    Arc::new(AppState {
        hot: HotCache::new(),
        cold,
        control: Control::new(),
        jwt_public_key,
    })
}
