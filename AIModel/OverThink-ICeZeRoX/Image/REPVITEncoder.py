import torch
import torch.nn as nn
import torch.nn.functional as F

from typing import List

from .REPVIT import Conv2d_BN, RepViTBlock, _make_divisible


class ICeZeRoX_REPVITEncoder(nn.Module):
    def __init__(self: ICeZeRoX_REPVITEncoder, in_channels: int, hidden_channels: int, blockcfgs: List[List[int]]) -> None:
        super().__init__()
        self.blockcfgs: List[List[int]] = blockcfgs

        input_channel: int = self.blockcfgs[0][2]
        self.patch_embed = nn.ModuleList([
            Conv2d_BN(in_channels, input_channel // 2, kernel_size=3, stride=2, padding=1),
            nn.GELU(),
            Conv2d_BN(input_channel // 2, input_channel, kernel_size=3, stride=2, padding=1),
        ])

        layers: List[nn.Module] = []
        output_channel: int = input_channel
        for cfg in self.blockcfgs:
            k, t, c, use_se, use_hs, s = cfg
            output_channel = _make_divisible(c, 8)
            exp_size = _make_divisible(input_channel * t, 8)
            layers.append(
                RepViTBlock(
                    in_channels=input_channel,
                    hidden_dim=exp_size,
                    out_channels=output_channel,
                    kernel_size=k,
                    stride=s,
                    use_se=bool(use_se),
                    use_hs=bool(use_hs),
                )
            )
            input_channel = output_channel

        self.blocks = nn.ModuleList(layers)
        self.out_proj = nn.Linear(output_channel, hidden_channels)

    def forward(self: ICeZeRoX_REPVITEncoder, x: torch.Tensor) -> torch.Tensor:
        for layer in self.patch_embed:
            x = layer(x)
        for block in self.blocks:
            x = block(x)
        x = F.adaptive_avg_pool2d(x, (1, 1)).flatten(1)
        x = self.out_proj(x)
        return x

    @torch.no_grad()
    def fuse(self: ICeZeRoX_REPVITEncoder) -> ICeZeRoX_REPVITEncoder:
        for i, layer in enumerate(self.patch_embed):
            if isinstance(layer, Conv2d_BN):
                self.patch_embed[i] = layer.fuse()
        for i, block in enumerate(self.blocks):
            if isinstance(block, RepViTBlock):
                self.blocks[i] = block.fuse()
        return self
