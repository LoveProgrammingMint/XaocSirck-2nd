use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use tokio::sync::Notify;
use tokio::time::{timeout, Duration};

pub const BUSY: u8 = 4;

pub struct Control {
    paused: AtomicBool,
    notify: Arc<Notify>,
}

impl Control {
    pub fn new() -> Self {
        Self {
            paused: AtomicBool::new(false),
            notify: Arc::new(Notify::new()),
        }
    }

    pub fn stop(&self) {
        self.paused.store(true, Ordering::SeqCst);
    }

    pub fn restart(&self) {
        self.paused.store(false, Ordering::SeqCst);
        self.notify.notify_waiters();
    }

    pub fn is_paused(&self) -> bool {
        self.paused.load(Ordering::SeqCst)
    }

    pub async fn wait(&self) -> Result<(), ()> {
        if !self.is_paused() {
            return Ok(());
        }
        timeout(Duration::from_secs(30), self.notify.notified())
            .await
            .map(|_| ())
            .map_err(|_| ())
    }
}
