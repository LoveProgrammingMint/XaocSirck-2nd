mod community;
mod routes;

pub use community::{Community, ReportEntry};
pub use routes::{admin_router, public_router, CommunityState};

use cache_service::AppState as CacheState;
use sqlx::PgPool;
use std::sync::Arc;

pub async fn create_state(pool: PgPool, cache: Arc<CacheState>) -> Arc<CommunityState> {
    let community = Community::new(pool)
        .await
        .expect("community init failed");
    Arc::new(CommunityState { community, cache })
}
