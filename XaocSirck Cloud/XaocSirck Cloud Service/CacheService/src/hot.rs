use ph::fmph::Function;
use std::fs::File;
use std::io::{BufReader, Write};
use std::path::Path;
use std::sync::Arc;
use tokio::sync::RwLock;

pub const MALICIOUS: u8 = 1;
pub const CLEAN: u8 = 0;
pub const NOT_FOUND: u8 = 2;

#[derive(Clone, Copy)]
pub enum HotKind {
    Malicious,
    Clean,
}

impl HotKind {
    pub fn path(self) -> &'static str {
        match self {
            HotKind::Malicious => "./MPHFs/HotMal.mphf",
            HotKind::Clean => "./MPHFs/HotCle.mphf",
        }
    }
}

pub struct HotCache {
    mal: Arc<RwLock<Option<Function>>>,
    cle: Arc<RwLock<Option<Function>>>,
}

impl HotCache {
    pub fn new() -> Self {
        Self {
            mal: Arc::new(RwLock::new(load_file(HotKind::Malicious.path()))),
            cle: Arc::new(RwLock::new(load_file(HotKind::Clean.path()))),
        }
    }

    pub async fn query(&self, sha256: &[u8]) -> u8 {
        if let Some(f) = self.mal.read().await.as_ref() {
            if f.get(sha256).is_some() {
                return MALICIOUS;
            }
        }
        if let Some(f) = self.cle.read().await.as_ref() {
            if f.get(sha256).is_some() {
                return CLEAN;
            }
        }
        NOT_FOUND
    }

    pub async fn update(&self, kind: HotKind, data: &[u8]) -> std::io::Result<()> {
        let path = kind.path();
        let mut file = File::create(path)?;
        file.write_all(data)?;
        file.flush()?;
        let new = tokio::task::block_in_place(|| load_file(kind.path()));
        let lock = match kind {
            HotKind::Malicious => &self.mal,
            HotKind::Clean => &self.cle,
        };
        *lock.write().await = new;
        Ok(())
    }
}

fn load_file<P: AsRef<Path>>(path: P) -> Option<Function> {
    let file = File::open(path).ok()?;
    Function::read(&mut BufReader::new(file)).ok()
}
