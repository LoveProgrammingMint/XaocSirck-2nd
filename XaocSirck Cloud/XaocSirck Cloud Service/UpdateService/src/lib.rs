mod routes;
mod update;

pub use routes::{admin_router, public_router, UpdateState};
pub use update::UpdateManager;

use std::sync::Arc;

pub fn create_state() -> Arc<UpdateState> {
    let manager = UpdateManager::new("./updates");
    Arc::new(UpdateState { manager })
}
