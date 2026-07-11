"""
Hybrid ML Models: LightGBM, CatBoost, and Stacking Ensemble.

This module provides implementations of LightGBM, CatBoost, and hybrid stacking
models with proper GPU fallback handling and clean numpy-array workflows.
"""

import warnings
import numpy as np
import lightgbm as lgb
from catboost import CatBoostClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedKFold
from sklearn.metrics import accuracy_score, f1_score, roc_auc_score
from sklearn.preprocessing import label_binarize
import torch
from typing import Any, Dict, List, Optional


def _get_lgb_device() -> str:
    """Return 'gpu' if CUDA is available, otherwise 'cpu'."""
    if torch.cuda.is_available():
        return "gpu"
    return "cpu"


def _compute_auc(y_true: np.ndarray, proba: np.ndarray, num_class: int) -> float:
    """Compute ROC-AUC score for binary or multiclass classification."""
    if num_class == 2:
        return float(roc_auc_score(y_true, proba[:, 1]))
    y_true_bin: np.ndarray = label_binarize(y_true, classes=list(range(num_class))) # type: ignore
    return float(roc_auc_score(y_true_bin, proba, multi_class="ovr", average="macro"))


class LightGBMModel:
    """LightGBM classifier with GPU fallback and feature-name handling."""

    def __init__(self, num_class: int = 10, n_estimators: int = 200) -> None:
        self.num_class: int = num_class
        device: str = _get_lgb_device()
        params: Dict[str, Any] = {
            "n_estimators": n_estimators,
            "learning_rate": 0.05,
            "max_depth": -1,
            "num_leaves": 31,
            "subsample": 0.8,
            "colsample_bytree": 0.8,
            "random_state": 42,
            "verbosity": -1,
            "n_jobs": -1,
            "device": device,
        }
        if num_class > 2:
            params["objective"] = "multiclass"
            params["num_class"] = num_class
        else:
            params["objective"] = "binary"
        self.model: lgb.LGBMClassifier = lgb.LGBMClassifier(**params)

    def fit(self, x_train: np.ndarray, y_train: np.ndarray) -> None:
        """Fit the model, falling back to CPU on GPU-specific failures."""
        # When trained with a pandas DataFrame, LightGBM records feature names
        # and sklearn warns if later given a numpy array without names. Only
        # pass explicit feature names when the training input exposes column
        # names (e.g. pandas DataFrame). For numpy arrays, call fit() without
        # feature names to avoid the sklearn "invalid feature names" warning.
        is_dataframe = hasattr(x_train, "columns")

        feature_names: Optional[List[str]] = None

        try:
            if is_dataframe:
                feature_names = list(x_train.columns) # type: ignore
                self.model.fit(x_train, y_train, feature_name=feature_names)
            else:
                self.model.fit(x_train, y_train)
        except lgb.basic.LightGBMError as exc:
            # Only catch LightGBM-specific GPU errors (e.g. OpenCL/CUDA build failure).
            err_msg: str = str(exc).lower()
            if "gpu" in err_msg or "cuda" in err_msg or "opencl" in err_msg or "device" in err_msg:
                warnings.warn(
                    f"LightGBM GPU training failed ({exc}), falling back to CPU",
                    RuntimeWarning,
                    stacklevel=2,
                )
                self.model.set_params(device="cpu")
                # Respect input type on fallback as well
                if is_dataframe:
                    self.model.fit(x_train, y_train, feature_name=feature_names) # type: ignore
                else:
                    self.model.fit(x_train, y_train)
            else:
                raise

    def predict_proba(self, x: np.ndarray) -> np.ndarray:
        return self.model.predict_proba(x) # type: ignore

    def evaluate(self, x_test: np.ndarray, y_test: np.ndarray) -> Dict[str, float]:
        pred: np.ndarray = self.model.predict(x_test) # type: ignore
        proba: np.ndarray = self.predict_proba(x_test)
        acc: float = float(accuracy_score(y_test, pred))
        f1: float = float(f1_score(y_test, pred, average="macro"))
        auc: float = _compute_auc(y_test, proba, self.num_class)
        return {"accuracy": acc, "f1": f1, "auc": auc}


class CatBoostModel:
    """CatBoost classifier with GPU fallback."""

    def __init__(self, num_class: int = 10, iterations: int = 200) -> None:
        self.num_class: int = num_class
        task_type: str = "GPU" if torch.cuda.is_available() else "CPU"
        loss_function: str = "MultiClass" if num_class > 2 else "Logloss"
        params: Dict[str, Any] = {
            "iterations": iterations,
            "learning_rate": 0.05,
            "depth": 6,
            "loss_function": loss_function,
            "random_seed": 42,
            "verbose": False,
            "thread_count": -1,
            "task_type": task_type,
        }
        if task_type.upper() == "GPU":
            params["devices"] = "0"
        if num_class > 2:
            params["classes_count"] = num_class
        self.model: CatBoostClassifier = CatBoostClassifier(**params)

    def fit(self, x_train: np.ndarray, y_train: np.ndarray) -> None:
        """Fit the model, falling back to CPU on GPU-specific failures."""
        try:
            self.model.fit(x_train, y_train)
        except Exception as exc:
            # CatBoost GPU failures raise generic Exception with CUDA/TCatBoostException
            # in the message. We check the message to decide whether to fall back.
            err_msg: str = str(exc).lower()
            if any(
                keyword in err_msg
                for keyword in ("cuda", "gpu", "tcatboostexception", "ncudalib")
            ):
                warnings.warn(
                    f"CatBoost GPU training failed ({exc}), falling back to CPU",
                    RuntimeWarning,
                    stacklevel=2,
                )
                self.model.set_params(task_type="CPU", devices=None)
                self.model.fit(x_train, y_train)
            else:
                raise

    def predict_proba(self, x: np.ndarray) -> np.ndarray:
        return self.model.predict_proba(x)

    def evaluate(self, x_test: np.ndarray, y_test: np.ndarray) -> Dict[str, float]:
        pred: np.ndarray = self.model.predict(x_test)
        # CatBoost predict() returns 2-D array for multiclass; flatten for binary.
        if pred.ndim > 1:
            pred = pred.flatten()
        proba: np.ndarray = self.predict_proba(x_test)
        acc: float = float(accuracy_score(y_test, pred))
        f1: float = float(f1_score(y_test, pred, average="macro"))
        auc: float = _compute_auc(y_test, proba, self.num_class)
        return {"accuracy": acc, "f1": f1, "auc": auc}


class HybridModel:
    """Simple ensemble averaging LightGBM and CatBoost probabilities via meta-learner."""

    def __init__(self, num_class: int = 10) -> None:
        self.num_class: int = num_class
        self.lgb_model: LightGBMModel = LightGBMModel(num_class=num_class)
        self.cb_model: CatBoostModel = CatBoostModel(num_class=num_class)
        self.meta_model: LogisticRegression = LogisticRegression(
            max_iter=1000,
            random_state=42,
        )

    def fit(self, x_train: np.ndarray, y_train: np.ndarray) -> None:
        self.lgb_model.fit(x_train, y_train)
        self.cb_model.fit(x_train, y_train)

        lgb_proba: np.ndarray = self.lgb_model.predict_proba(x_train)
        cb_proba: np.ndarray = self.cb_model.predict_proba(x_train)
        meta_features: np.ndarray = np.hstack([lgb_proba, cb_proba])

        self.meta_model.fit(meta_features, y_train)

    def predict_proba(self, x: np.ndarray) -> np.ndarray:
        lgb_proba: np.ndarray = self.lgb_model.predict_proba(x)
        cb_proba: np.ndarray = self.cb_model.predict_proba(x)
        meta_features: np.ndarray = np.hstack([lgb_proba, cb_proba])
        return self.meta_model.predict_proba(meta_features)

    def evaluate(self, x_test: np.ndarray, y_test: np.ndarray) -> Dict[str, float]:
        proba: np.ndarray = self.predict_proba(x_test)
        pred: np.ndarray = np.argmax(proba, axis=1)
        acc: float = float(accuracy_score(y_test, pred))
        f1: float = float(f1_score(y_test, pred, average="macro"))
        auc: float = _compute_auc(y_test, proba, self.num_class)
        return {"accuracy": acc, "f1": f1, "auc": auc}


class HybridStackingModel:
    """Stacking ensemble with out-of-fold meta-features and weighted blending."""

    def __init__(self, num_class: int = 10, n_splits: int = 5) -> None:
        self.num_class: int = num_class
        self.n_splits: int = n_splits
        self.lgb_model: LightGBMModel = LightGBMModel(
            num_class=num_class, n_estimators=300
        )
        self.cb_model: CatBoostModel = CatBoostModel(
            num_class=num_class, iterations=300
        )
        self.meta_model: LogisticRegression = LogisticRegression(
            max_iter=2000,
            C=0.5,
            solver="lbfgs",
            random_state=42,
        )
        self.best_weight: float = 0.5

    def _log_loss(self, y_true: np.ndarray, proba: np.ndarray) -> float:
        """Compute log-loss (cross-entropy) for binary or multiclass."""
        eps: float = 1e-15
        proba = np.clip(proba, eps, 1.0 - eps)
        if self.num_class == 2:
            return -np.mean(
                y_true * np.log(proba[:, 1]) + (1 - y_true) * np.log(proba[:, 0])
            )
        y_true_bin: np.ndarray = label_binarize(
            y_true, classes=list(range(self.num_class)) # type: ignore
        )
        return -np.mean(np.sum(y_true_bin * np.log(proba), axis=1))

    def fit(self, x_train: np.ndarray, y_train: np.ndarray) -> None:
        n_samples: int = int(x_train.shape[0])
        lgb_oof: np.ndarray = np.zeros(
            (n_samples, self.num_class), dtype=np.float32
        )
        cb_oof: np.ndarray = np.zeros(
            (n_samples, self.num_class), dtype=np.float32
        )

        kfold: StratifiedKFold = StratifiedKFold(
            n_splits=self.n_splits, shuffle=True, random_state=42
        )
        for train_idx, valid_idx in kfold.split(x_train, y_train):
            x_tr: np.ndarray = x_train[train_idx]
            y_tr: np.ndarray = y_train[train_idx]
            x_val: np.ndarray = x_train[valid_idx]

            lgb_fold: LightGBMModel = LightGBMModel(
                num_class=self.num_class, n_estimators=300
            )
            lgb_fold.fit(x_tr, y_tr)
            lgb_oof[valid_idx] = lgb_fold.predict_proba(x_val)

            cb_fold: CatBoostModel = CatBoostModel(
                num_class=self.num_class, iterations=300
            )
            cb_fold.fit(x_tr, y_tr)
            cb_oof[valid_idx] = cb_fold.predict_proba(x_val)

        meta_features: np.ndarray = np.hstack([lgb_oof, cb_oof, x_train])
        meta_features = np.nan_to_num(
            meta_features, nan=0.0, posinf=0.0, neginf=0.0
        )
        self.meta_model.fit(meta_features, y_train)

        best_loss: float = float("inf")
        best_w: float = 0.5
        for w in np.linspace(0.0, 1.0, 51):
            avg_proba: np.ndarray = w * lgb_oof + (1.0 - w) * cb_oof
            loss: float = self._log_loss(y_train, avg_proba)
            if loss < best_loss:
                best_loss = loss
                best_w = w
        self.best_weight = best_w

        self.lgb_model.fit(x_train, y_train)
        self.cb_model.fit(x_train, y_train)

    def predict_proba(self, x: np.ndarray) -> np.ndarray:
        lgb_proba: np.ndarray = np.nan_to_num(
            self.lgb_model.predict_proba(x), nan=0.0, posinf=0.0, neginf=0.0
        )
        cb_proba: np.ndarray = np.nan_to_num(
            self.cb_model.predict_proba(x), nan=0.0, posinf=0.0, neginf=0.0
        )
        x_clean: np.ndarray = np.nan_to_num(
            x, nan=0.0, posinf=0.0, neginf=0.0
        )
        meta_features: np.ndarray = np.hstack([lgb_proba, cb_proba, x_clean])
        stacking_proba: np.ndarray = self.meta_model.predict_proba(meta_features)
        avg_proba: np.ndarray = (
            self.best_weight * lgb_proba + (1.0 - self.best_weight) * cb_proba
        )
        return 0.5 * stacking_proba + 0.5 * avg_proba

    def evaluate(self, x_test: np.ndarray, y_test: np.ndarray) -> Dict[str, float]:
        proba: np.ndarray = self.predict_proba(x_test)
        pred: np.ndarray = np.argmax(proba, axis=1)
        acc: float = float(accuracy_score(y_test, pred))
        f1: float = float(f1_score(y_test, pred, average="macro"))
        auc: float = _compute_auc(y_test, proba, self.num_class)
        return {"accuracy": acc, "f1": f1, "auc": auc}