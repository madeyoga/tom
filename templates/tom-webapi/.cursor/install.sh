#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="/usr/local/bin:${DOTNET_ROOT}:${PATH}"

ASPIRE_DASHBOARD_VERSION=13.6.0
ASPIRE_DASHBOARD_SHA256=e5a603eb9c0a7982fefd8323576befa63c4a0b023ffe909bec4513a2dc4df4b3

install_packages() {
  local missing=()
  local pkg
  for pkg in ca-certificates curl git unzip xz-utils build-essential python3 pkg-config libicu74; do
    if ! dpkg -s "$pkg" >/dev/null 2>&1; then
      missing+=("$pkg")
    fi
  done
  if ((${#missing[@]} > 0)); then
    sudo apt-get update
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "${missing[@]}"
  fi
}

install_dotnet() {
  if [[ -x "${DOTNET_ROOT}/dotnet" ]] && "${DOTNET_ROOT}/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
    sudo ln -sfn "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet
    return
  fi
  sudo mkdir -p "${DOTNET_ROOT}"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  sudo bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "${DOTNET_ROOT}"
  sudo ln -sfn "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet
  rm -f /tmp/dotnet-install.sh
}

write_shell_path() {
  sudo tee /etc/profile.d/dev-toolchain.sh >/dev/null <<'EOF'
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="/usr/local/bin:/usr/local/share/dotnet:${PATH}"
EOF
  if [[ -f "${HOME}/.bashrc" ]] && ! grep -q 'DOTNET_ROOT=/usr/local/share/dotnet' "${HOME}/.bashrc"; then
    cat >> "${HOME}/.bashrc" <<'EOF'
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="/usr/local/bin:/usr/local/share/dotnet:$PATH"
EOF
  fi
}

install_aspire_dashboard() {
  local dest="/opt/aspire-dashboard/${ASPIRE_DASHBOARD_VERSION}"
  local bin="${dest}/tools/Aspire.Dashboard"
  if [[ -x "$bin" ]]; then
    return
  fi

  local url="https://api.nuget.org/v3-flatcontainer/aspire.dashboard.sdk.linux-x64/${ASPIRE_DASHBOARD_VERSION}/aspire.dashboard.sdk.linux-x64.${ASPIRE_DASHBOARD_VERSION}.nupkg"
  local tmp stage
  tmp="$(mktemp)"
  stage="$(mktemp -d)"
  curl -fsSL "$url" -o "$tmp"
  echo "${ASPIRE_DASHBOARD_SHA256}  ${tmp}" | sha256sum -c -
  unzip -q "$tmp" "tools/*" -d "$stage"
  rm -f "$tmp"
  chmod +x "${stage}/tools/Aspire.Dashboard"
  sudo rm -rf "$dest"
  sudo mkdir -p /opt/aspire-dashboard
  sudo mv "$stage" "$dest"
}

install_packages
install_dotnet
write_shell_path
install_aspire_dashboard

dotnet restore Tom.WebApi.slnx
