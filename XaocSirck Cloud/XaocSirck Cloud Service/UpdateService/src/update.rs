use std::path::{Path, PathBuf};

pub struct UpdateManager {
    dir: PathBuf,
}

impl UpdateManager {
    pub fn new<P: AsRef<Path>>(dir: P) -> Self {
        let dir = dir.as_ref().to_path_buf();
        let _ = std::fs::create_dir_all(&dir);
        Self { dir }
    }

    pub fn dir(&self) -> &Path {
        &self.dir
    }

    pub fn package_path(&self) -> PathBuf {
        self.dir.join("latest.zip")
    }

    pub fn version_path(&self) -> PathBuf {
        self.dir.join("version.txt")
    }

    pub fn version(&self) -> String {
        std::fs::read_to_string(self.version_path())
            .unwrap_or_else(|_| "0.0.0".to_string())
            .trim()
            .to_string()
    }

    pub fn save(&self, data: &[u8], version: &str) -> std::io::Result<()> {
        std::fs::write(self.package_path(), data)?;
        std::fs::write(self.version_path(), version)?;
        Ok(())
    }
}
