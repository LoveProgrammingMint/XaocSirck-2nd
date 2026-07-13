use axum::extract::Request;
use axum::http::StatusCode;
use axum::middleware::Next;
use axum::response::IntoResponse;
use common::auth;
use std::sync::Arc;

pub async fn require_auth(
    request: Request,
    next: Next,
) -> Result<impl IntoResponse, impl IntoResponse> {
    let headers = request.headers();
    let header = headers
        .get("authorization")
        .and_then(|value| value.to_str().ok())
        .ok_or_else(|| (StatusCode::UNAUTHORIZED, "Unauthorized"))?;
    let token = header
        .strip_prefix("Bearer ")
        .ok_or_else(|| (StatusCode::UNAUTHORIZED, "Unauthorized"))?;

    let public_key = request
        .extensions()
        .get::<Arc<String>>()
        .cloned()
        .unwrap_or_else(|| Arc::new(String::new()));

    match auth::verify(token, public_key.as_bytes()) {
        Ok(claims) => {
            let mut request = request;
            request.extensions_mut().insert(claims);
            Ok(next.run(request).await)
        }
        Err(_) => Err((StatusCode::UNAUTHORIZED, "Unauthorized")),
    }
}
