mod cloud;
mod routes;

pub use cloud::SignatureCloud;
pub use routes::{admin_router, public_router, SignatureState};

use sqlx::PgPool;
use std::sync::Arc;

pub async fn create_state(pool: PgPool, jwt_public_key: String) -> Arc<SignatureState> {
    let cloud = SignatureCloud::new(pool)
        .await
        .expect("signature cloud init failed");
    Arc::new(SignatureState {
        cloud,
        jwt_public_key,
    })
}
