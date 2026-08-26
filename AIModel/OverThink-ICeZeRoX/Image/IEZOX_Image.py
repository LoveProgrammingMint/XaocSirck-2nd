import torch
import torch.nn as nn

from typing import Dict, Any, Tuple

from .REPVITEncoder import ICeZeRoX_REPVITEncoder


class IEZOX_IMAGE_MULTIMODAL(nn.Module):
    def __init__(self: IEZOX_IMAGE_MULTIMODAL, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.BEVI: Dict[str, Any] = settings["BEVI"]
        self.PESSI: Dict[str, Any] = settings["PESSI"]
        self.RBGI: Dict[str, Any] = settings["RBGI"]

        self.BEVI_Encoder = ICeZeRoX_REPVITEncoder(self.BEVI["IN_CHANNELS"], self.BEVI["HIDDEN_CHANNELS"], self.BEVI["BLOCK_CFGS"])
        self.PESSI_Encoder = ICeZeRoX_REPVITEncoder(self.PESSI["IN_CHANNELS"], self.PESSI["HIDDEN_CHANNELS"], self.PESSI["BLOCK_CFGS"])
        self.RBGI_Encoder = ICeZeRoX_REPVITEncoder(self.RBGI["IN_CHANNELS"], self.RBGI["HIDDEN_CHANNELS"], self.RBGI["BLOCK_CFGS"])

    def forward(self: IEZOX_IMAGE_MULTIMODAL, BEVI_x: torch.Tensor, PESSI_x: torch.Tensor, RBGI_x: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        BEVI_out = self.BEVI_Encoder(BEVI_x)
        PESSI_out = self.PESSI_Encoder(PESSI_x)
        RBGI_out = self.RBGI_Encoder(RBGI_x)
        return BEVI_out, PESSI_out, RBGI_out


class IEZOX_IMAGE_CONFLUENCE(nn.Module):
    def __init__(self: IEZOX_IMAGE_CONFLUENCE, settings: Dict[str, Any]) -> None:
        super().__init__()
        self.d_model: int = settings["D_MODEL"]
        self.in_dim: int = settings["IN_DIM"]
        self.out_dim: int = settings["OUT_DIM"]
        self.num_layers: int = settings["NUM_LAYERS"]
        self.num_heads: int = settings["NUM_HEADS"]
        self.dim_feedforward: int = settings["DIM_FEEDFORWARD"]
        self.dropout: float = settings["DROPOUT"]
        self.modality_dropout: float = settings["MODALITY_DROPOUT"]

        self.modal_embed = nn.Parameter(torch.randn(1, 3, self.d_model) * 0.02)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model, nhead=self.num_heads, dim_feedforward=self.dim_feedforward,
            dropout=self.dropout, activation="gelu", batch_first=True, norm_first=True)
        self.transformer_encoder = nn.TransformerEncoder(encoder_layer, num_layers=self.num_layers)

        self.in_proj = nn.Linear(self.in_dim, self.d_model)
        self.out_proj = nn.Linear(self.d_model, self.out_dim)

    def forward(self: IEZOX_IMAGE_CONFLUENCE, BEVI_out: torch.Tensor, PESSI_out: torch.Tensor, RBGI_out: torch.Tensor) -> torch.Tensor:
        x = torch.stack([BEVI_out, PESSI_out, RBGI_out], dim=1)
        x = self.in_proj(x)
        x = x + self.modal_embed

        if self.training and self.modality_dropout > 0:
            mask = torch.bernoulli(torch.full((x.size(0), 3), 1 - self.modality_dropout)).to(x.device)
            fill = self.modal_embed.expand(x.size(0), -1, -1)
            x = torch.where(mask.unsqueeze(-1).bool(), x, fill * 0.1)

        x = self.transformer_encoder(x)
        x = x.mean(dim=1)
        return self.out_proj(x)
