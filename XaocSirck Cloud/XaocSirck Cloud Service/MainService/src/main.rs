use axum::{middleware, routing::delete, routing::get, Extension, Router};
use cache_service::create_state_with_pool;
use common::{Settings, CACHE_ROUTE_PREFIX, CACHE_SERVICE_BIND, SIGNATURE_ROUTE_PREFIX, SYSTEM_ROUTE_PREFIX, UPDATE_ROUTE_PREFIX};
use signature_service::create_state as create_signature_state;
use sqlx::PgPool;
use std::sync::Arc;
use tokio::net::TcpListener;

mod auth;
mod system;

fn spawn_hot_cache_builder(state: Arc<cache_service::AppState>) {
    tokio::spawn(async move {
        loop {
            let now = chrono::Utc::now();
            let tomorrow = now.date_naive().succ_opt().unwrap_or(now.date_naive());
            let midnight = tomorrow.and_hms_opt(0, 0, 0).unwrap_or_else(|| now.naive_utc());
            let next = match midnight.and_local_timezone(chrono::Utc) {
                chrono::LocalResult::Single(t) => t,
                _ => now + chrono::Duration::hours(24),
            };
            let wait = (next - now).to_std().unwrap_or(std::time::Duration::from_secs(60));
            tokio::time::sleep(wait).await;
            match cache_service::build_hot_cache(&state).await {
                Ok(body) => eprintln!("auto hot cache built: {}", body),
                Err(e) => eprintln!("auto hot cache build failed: {}", e),
            }
            tokio::time::sleep(std::time::Duration::from_secs(24 * 60 * 60)).await;
        }
    });
}

#[tokio::main]
async fn main() {
    let settings = Settings::load("./settings.json").expect("load settings failed");
    let pool = PgPool::connect(&settings.database_url)
        .await
        .expect("database connect failed");

    let jwt_key = Arc::new(settings.jwt_public_key.clone());
    let cache_state = create_state_with_pool(pool.clone(), settings.jwt_public_key.clone()).await;
    let signature_state = create_signature_state(pool.clone(), settings.jwt_public_key.clone()).await;
    let update_state = update_service::create_state();
    let system_state = system::init(pool).await.expect("system init failed");

    spawn_hot_cache_builder(cache_state.clone());

    let cache_router = cache_service::public_router(cache_state.clone()).merge(
        cache_service::admin_router(cache_state)
            .layer(middleware::from_fn(auth::require_auth)),
    );
    let signature_router = signature_service::public_router(signature_state.clone()).merge(
        signature_service::admin_router(signature_state)
            .layer(middleware::from_fn(auth::require_auth)),
    );
    let update_router = update_service::public_router(update_state.clone()).merge(
        update_service::admin_router(update_state)
            .layer(middleware::from_fn(auth::require_auth)),
    );
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
        .nest(SIGNATURE_ROUTE_PREFIX, signature_router)
        .nest(UPDATE_ROUTE_PREFIX, update_router)
        .nest(SYSTEM_ROUTE_PREFIX, system_router)
        .layer(middleware::from_fn(system::stats::log_request))
        .layer(middleware::from_fn(system::stats::check_blacklist))
        .layer(Extension(system_state))
        .layer(Extension(jwt_key));

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
