import copy
import torch
import torch.nn as nn


def reparam(model: nn.Module) -> nn.Module:
    fused: nn.Module = copy.deepcopy(model).eval()
    _reparam_inplace(fused)
    return fused


def _reparam_inplace(module: nn.Module) -> None:
    fuse_fn = getattr(module, "fuse", None)
    if callable(fuse_fn):
        with torch.no_grad():
            fuse_fn()
        return
    for child in list(module.children()):
        _reparam_inplace(child)
