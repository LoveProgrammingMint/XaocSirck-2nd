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
use std::sync::Arc;

pub async fn create_state(settings: &Settings) -> Arc<AppState> {
    let cold = ColdCache::connect(&settings.database_url)
        .await
        .expect("cold cache connect failed");
    Arc::new(AppState {
        hot: HotCache::new(),
        cold,
        control: Control::new(),
        jwt_public_key: settings.jwt_public_key.clone(),
    })
}
