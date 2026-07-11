use axum::Router;
use cache_service::create_state;
use common::{Settings, CACHE_SERVICE_BIND, CACHE_ROUTE_PREFIX};
use tokio::net::TcpListener;

#[tokio::main]
async fn main() {
    let settings = Settings::load("./settings.json").expect("load settings failed");
    let state = create_state(&settings).await;
    let app = Router::new().nest(CACHE_ROUTE_PREFIX, cache_service::router(state));
    let listener = TcpListener::bind(CACHE_SERVICE_BIND).await.expect("bind failed");
    axum::serve(listener, app).await.expect("serve failed");
}
