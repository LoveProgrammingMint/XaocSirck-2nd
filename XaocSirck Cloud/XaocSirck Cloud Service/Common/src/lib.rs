pub mod auth;

use serde::Deserialize;
use std::fs;
use std::path::Path;

pub const CACHE_SERVICE_BIND: &str = "0.0.0.0:5100";
pub const CACHE_ROUTE_PREFIX: &str = "/api/cache";
pub const SYSTEM_ROUTE_PREFIX: &str = "/api/system";
pub const SIGNATURE_ROUTE_PREFIX: &str = "/api/signature";

#[derive(Deserialize, Clone)]
pub struct Settings {
    pub database_url: String,
    pub jwt_public_key: String,
}

impl Settings {
    pub fn load<P: AsRef<Path>>(path: P) -> Result<Self, Box<dyn std::error::Error>> {
        let content = fs::read_to_string(path)?;
        let settings: Settings = serde_json::from_str(&content)?;
        Ok(settings)
    }
}
