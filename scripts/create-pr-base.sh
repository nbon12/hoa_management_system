#!/usr/bin/env bash
# Create the long-lived, pre-seeded `pr-base` Neon branch that every per-PR environment forks from
# (013 T009, research D3). Run ONCE by an operator (or by the sweep's self-heal). After creation, the
# branch must be SEEDED with the deterministic seed — point a one-off app run (ASPNETCORE_ENVIRONMENT=Dev,
# ConnectionStrings__DefaultConnection = this branch) at it, or `dotnet run -- --seed`, so forks start
# ready. Print the resulting branch id and set it as the GH secret NEON_PR_BASE_BRANCH_ID.
#
# Env:
#   NEON_API_KEY        Neon API key
#   NEON_PROJECT_ID     shared Neon project id (super-water-18090867)
#   PARENT_BRANCH_ID    branch to fork pr-base from (the Dev `main` branch id; Dev-shaped schema)
set -euo pipefail
: "${NEON_API_KEY:?}" "${NEON_PROJECT_ID:?}" "${PARENT_BRANCH_ID:?}"

api() { curl -sS -H "Authorization: Bearer ${NEON_API_KEY}" -H "Content-Type: application/json" "$@"; }

# Idempotent: reuse an existing branch named pr-base.
existing="$(api "https://console.neon.tech/api/v2/projects/${NEON_PROJECT_ID}/branches" \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print(next((b['id'] for b in d.get('branches',[]) if b.get('name')=='pr-base'),''))")"

if [ -n "$existing" ]; then
  echo "pr-base already exists: ${existing}"
else
  existing="$(api -X POST "https://console.neon.tech/api/v2/projects/${NEON_PROJECT_ID}/branches" \
    -d "{\"branch\":{\"name\":\"pr-base\",\"parent_id\":\"${PARENT_BRANCH_ID}\"},\"endpoints\":[{\"type\":\"read_write\"}]}" \
    | python3 -c "import sys,json;print(json.load(sys.stdin).get('branch',{}).get('id',''))")"
  echo "Created pr-base: ${existing}"
fi

echo
echo "Next steps:"
echo "  1. Seed it: run the app once with ConnectionStrings__DefaultConnection pointed at pr-base (or dotnet run -- --seed)."
echo "  2. Set the GitHub secret:  NEON_PR_BASE_BRANCH_ID=${existing}"
