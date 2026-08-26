import torch
import torch.nn as nn
import torch.nn.functional as F

from timm.layers.squeeze_excite import SqueezeExcite

from typing import Optional, Callable


def _make_act(use_hs: bool) -> nn.Module:
    return nn.SiLU() if use_hs else nn.GELU()


def _make_divisible(v: float, divisor: int, min_value: Optional[int] = None) -> int:
    if min_value is None:
        min_value = divisor
    new_v = max(min_value, int(v + divisor / 2) // divisor * divisor)
    if new_v < 0.9 * v:
        new_v += divisor
    return new_v


class Conv2d_BN(nn.Module):
    def __init__(
        self,
        in_channels: int,
        out_channels: int,
        kernel_size: int = 1,
        stride: int = 1,
        padding: int = 0,
        dilation: int = 1,
        groups: int = 1,
        bn_weight_init: float = 1.0,
    ) -> None:
        super().__init__()
        self.conv = nn.Conv2d(
            in_channels=in_channels,
            out_channels=out_channels,
            kernel_size=kernel_size,
            stride=stride,
            padding=padding,
            dilation=dilation,
            groups=groups,
            bias=False,
        )
        self.bn = nn.BatchNorm2d(num_features=out_channels)
        nn.init.constant_(self.bn.weight, bn_weight_init)
        nn.init.constant_(self.bn.bias, 0)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.bn(self.conv(x))

    @torch.no_grad()
    def fuse(self) -> nn.Conv2d:
        c = self.conv
        bn = self.bn
        assert bn.running_var is not None, "BatchNorm running_var must be available for fusion"
        assert bn.running_mean is not None, "BatchNorm running_mean must be available for fusion"

        w = bn.weight / (bn.running_var + bn.eps) ** 0.5
        w = c.weight * w[:, None, None, None]
        b = bn.bias - bn.running_mean * bn.weight / (bn.running_var + bn.eps) ** 0.5
        fused = nn.Conv2d(
            in_channels=w.size(1) * c.groups,
            out_channels=w.size(0),
            kernel_size=(int(w.shape[2]), int(w.shape[3])),
            stride=(c.stride[0], c.stride[1]),
            padding=(int(c.padding[0]), int(c.padding[1])),
            dilation=(int(c.dilation[0]), int(c.dilation[1])),
            groups=c.groups,
            bias=True,
            device=c.weight.device,
        )
        fused.weight.data.copy_(w)
        assert fused.bias is not None, "Fused Conv2d must have bias"
        fused.bias.data.copy_(b)
        return fused


class RepVGGDW(nn.Module):
    def __init__(self, channels: int) -> None:
        super().__init__()
        self.conv3x3 = Conv2d_BN(channels, channels, kernel_size=3, stride=1, padding=1, groups=channels)
        self.conv1x1 = nn.Conv2d(channels, channels, kernel_size=1, stride=1, padding=0, groups=channels)
        self.bn = nn.BatchNorm2d(channels)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.bn(self.conv3x3(x) + self.conv1x1(x) + x)

    @torch.no_grad()
    def fuse(self) -> nn.Conv2d:
        conv3x3 = self.conv3x3.fuse()
        conv1x1 = self.conv1x1

        conv1x1_w = F.pad(conv1x1.weight, [1, 1, 1, 1])
        conv1x1_b = conv1x1.bias if conv1x1.bias is not None else torch.zeros(conv1x1.weight.size(0), device=conv1x1.weight.device)

        identity = F.pad(
            torch.ones(conv1x1_w.shape[0], conv1x1_w.shape[1], 1, 1, device=conv1x1_w.device),
            [1, 1, 1, 1],
        )

        final_w = conv3x3.weight + conv1x1_w + identity
        assert conv3x3.bias is not None, "conv3x3 must have bias after fusion"
        final_b = conv3x3.bias + conv1x1_b

        conv3x3.weight.data.copy_(final_w)
        conv3x3.bias.data.copy_(final_b)

        bn = self.bn
        assert bn.running_var is not None, "BatchNorm running_var must be available for fusion"
        w = bn.weight / (bn.running_var + bn.eps) ** 0.5
        w = conv3x3.weight * w[:, None, None, None]
        assert bn.running_mean is not None, "BatchNorm running_mean must be available for fusion"
        b = bn.bias + (conv3x3.bias - bn.running_mean) * bn.weight / (bn.running_var + bn.eps) ** 0.5

        conv3x3.weight.data.copy_(w)
        conv3x3.bias.data.copy_(b)
        return conv3x3


class RepViTBlock(nn.Module):
    def __init__(
        self,
        in_channels: int,
        hidden_dim: int,
        out_channels: int,
        kernel_size: int,
        stride: int,
        use_se: bool,
        use_hs: bool,
    ) -> None:
        super().__init__()
        assert stride in [1, 2], "stride must be 1 or 2"
        self.identity: bool = (stride == 1 and in_channels == out_channels)
        assert hidden_dim == 2 * in_channels, "hidden_dim must be 2 * in_channels"

        self.stride: int = stride
        self.in_channels: int = in_channels
        self.out_channels: int = out_channels
        self.use_hs: bool = use_hs

        if stride == 2:
            self.token_mixer = nn.ModuleList([
                Conv2d_BN(
                    in_channels=in_channels,
                    out_channels=in_channels,
                    kernel_size=kernel_size,
                    stride=stride,
                    padding=(kernel_size - 1) // 2,
                    groups=in_channels,
                ),
            ])
            self.token_mixer.append(SqueezeExcite(in_channels, rd_ratio=0.25) if use_se else nn.Identity())
            self.token_mixer.append(
                Conv2d_BN(
                    in_channels=in_channels,
                    out_channels=out_channels,
                    kernel_size=1,
                    stride=1,
                    padding=0,
                )
            )

            self.channel_mixer = nn.ModuleList([
                Conv2d_BN(out_channels, 2 * out_channels, kernel_size=1, stride=1, padding=0),
                _make_act(use_hs),
                Conv2d_BN(2 * out_channels, out_channels, kernel_size=1, stride=1, padding=0, bn_weight_init=0),
            ])
        else:
            assert self.identity, "stride=1 requires in_channels == out_channels"
            self.token_mixer = nn.ModuleList([
                RepVGGDW(in_channels),
            ])
            self.token_mixer.append(SqueezeExcite(in_channels, rd_ratio=0.25) if use_se else nn.Identity())

            self.channel_mixer = nn.ModuleList([
                Conv2d_BN(in_channels, hidden_dim, kernel_size=1, stride=1, padding=0),
                _make_act(use_hs),
                Conv2d_BN(hidden_dim, out_channels, kernel_size=1, stride=1, padding=0, bn_weight_init=0),
            ])

    def _token_mixer_forward(self, x: torch.Tensor) -> torch.Tensor:
        for layer in self.token_mixer:
            x = layer(x)
        return x

    def _channel_mixer_forward(self, x: torch.Tensor) -> torch.Tensor:
        residual: torch.Tensor = x
        for layer in self.channel_mixer:
            x = layer(x)
        if self.identity:
            x = x + residual
        return x

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = self._token_mixer_forward(x)
        x = self._channel_mixer_forward(x)
        return x

    @torch.no_grad()
    def fuse(self) -> nn.Module:
        for i, layer in enumerate(self.token_mixer):
            if isinstance(layer, Conv2d_BN):
                self.token_mixer[i] = layer.fuse()
            elif isinstance(layer, RepVGGDW):
                self.token_mixer[i] = layer.fuse()

        for i, layer in enumerate(self.channel_mixer):
            if isinstance(layer, Conv2d_BN):
                self.channel_mixer[i] = layer.fuse()
        return self
