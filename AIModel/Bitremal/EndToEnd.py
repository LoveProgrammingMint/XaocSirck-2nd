from typing import Optional, Tuple

import torch
from torch import nn
from torch.nn import functional as F

import numpy as np

from Bitremal.components.RawByte import RawByteEmbedding, RawByteBackbone, bytes_to_input_ids
from Bitremal.components.AssemblyList import AssemblyListEmbedding, AssemblyListBackbone, ids_to_input_ids
from Bitremal.components.EntropyMap import EntropyMapEncoder
from Bitremal.components.ImportTable import ImportTableEncoder
from Bitremal.Bitremal import Bitremal

class EndToEnd(nn.Module):
    def __init__(self, num_classes: int = 2) -> None:
        super().__init__()
        self.rb_embedding: RawByteEmbedding = RawByteEmbedding()
        self.al_embedding: AssemblyListEmbedding = AssemblyListEmbedding()
        self.rb_backbone: RawByteBackbone = RawByteBackbone()
        self.al_backbone: AssemblyListBackbone = AssemblyListBackbone()
        self.em_encoder: EntropyMapEncoder = EntropyMapEncoder(output_dim=128)
        self.it_encoder: ImportTableEncoder = ImportTableEncoder(output_dim=128)
        self.rb_head: nn.Linear = nn.Linear(128, num_classes)
        self.al_head: nn.Linear = nn.Linear(128, num_classes)
        self.bitremal: Bitremal = Bitremal(num_classes=num_classes)

    def forward(self, rb_input: torch.Tensor, al_input: torch.Tensor, em_input: torch.Tensor, it_input: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        rb_embedded: torch.Tensor = self.rb_embedding(rb_input)
        al_embedded: torch.Tensor = self.al_embedding(al_input)
        rb_feature: torch.Tensor = self.rb_backbone(rb_embedded)
        al_feature: torch.Tensor = self.al_backbone(al_embedded)
        em_feature: torch.Tensor = self.em_encoder(em_input)
        it_feature: torch.Tensor = self.it_encoder(it_input)
        rb_logits: torch.Tensor = self.rb_head(rb_feature)
        al_logits: torch.Tensor = self.al_head(al_feature)
        merged: torch.Tensor = torch.cat([rb_feature, al_feature, em_feature, it_feature], dim=-1)
        bitremal_logits: torch.Tensor = self.bitremal(merged)
        return rb_logits, al_logits, bitremal_logits

    def get_components(self) -> dict[str, nn.Module]:
        return {
            "rb_embedding": self.rb_embedding,
            "al_embedding": self.al_embedding,
            "rb_backbone": self.rb_backbone,
            "al_backbone": self.al_backbone,
            "em_encoder": self.em_encoder,
            "it_encoder": self.it_encoder,
            "rb_head": self.rb_head,
            "al_head": self.al_head,
            "bitremal": self.bitremal,
        }