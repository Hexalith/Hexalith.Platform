#!/bin/bash
export HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons
dotnet build "$1" -c Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=/home/administrator/hexalith-protection-worktrees/ext-protection-loop3-b8fd390531/eventstore -p:HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons -p:HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src -m:1 -nodeReuse:false
