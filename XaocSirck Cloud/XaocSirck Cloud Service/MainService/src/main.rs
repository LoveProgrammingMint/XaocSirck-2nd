use axum::{middleware, routing::delete, routing::get, Router};
use cache_service::create_state_with_pool;
use common::{Settings, CACHE_ROUTE_PREFIX, CACHE_SERVICE_BIND, SYSTEM_ROUTE_PREFIX};
use sqlx::PgPool;
use tokio::net::TcpListener;

mod system;

#[tokio::main]
async fn main() {
    let settings = Settings::load("./settings.json").expect("load settings failed");
    let pool = PgPool::connect(&settings.database_url)
        .await
        .expect("database connect failed");

    let cache_state = create_state_with_pool(pool.clone(), settings.jwt_public_key.clone()).await;
    let system_state = system::init(pool).await.expect("system init failed");

    let cache_router = cache_service::router(cache_state);
    let system_router = Router::new()
        .route("/stats", get(system::stats::stats))
        .route("/health", get(system::health::health))
        .route("/histogram", get(system::stats::histogram))
        .route("/routes", get(system::routes::route_stats))
        .route(
            "/errors",
            get(system::errors::list).post(system::errors::create),
        )
        .route("/ips", get(system::security::ip_stats))
        .route(
            "/blacklist",
            get(system::security::list_blacklist).post(system::security::add_blacklist),
        )
        .route("/blacklist/remove", delete(system::security::remove_blacklist))
        .with_state(system_state.clone());

    let app = Router::new()
        .nest(CACHE_ROUTE_PREFIX, cache_router)
        .nest(SYSTEM_ROUTE_PREFIX, system_router)
        .layer(middleware::from_fn_with_state(
            system_state.clone(),
            system::stats::log_request,
        ))
        .layer(middleware::from_fn_with_state(
            system_state,
            system::stats::check_blacklist,
        ));

    let listener = TcpListener::bind(CACHE_SERVICE_BIND)
        .await
        .expect("bind failed");
    axum::serve(
        listener,
        app.into_make_service_with_connect_info::<std::net::SocketAddr>(),
    )
    .await
    .expect("serve failed");
}
