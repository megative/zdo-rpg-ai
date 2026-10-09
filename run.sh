#!/usr/bin/env bash
# Starts the server and the client together; Ctrl+C stops both.
# Configs: .tmp/server-config.yaml and .tmp/client-config.yaml
set -eu
cd "$(dirname "$0")"

# The projects target net9.0; this lets them run on a newer .NET runtime too
export DOTNET_ROLL_FORWARD=Major

dotnet build ZdoRpgAi.sln -nologo -v q

dotnet src/ZdoRpgAi.Server.Console/bin/Debug/net9.0/ZdoRpgAi.Server.Console.dll --config .tmp/server-config.yaml \
    > >(sed -u 's/^/[server] /') 2>&1 &
server=$!
# Background jobs of a script ignore Ctrl+C, so the server is stopped explicitly
trap 'kill "$server" 2>/dev/null; wait "$server" 2>/dev/null' EXIT

dotnet src/ZdoRpgAi.Client.Console/bin/Debug/net9.0/ZdoRpgAi.Client.Console.dll --config .tmp/client-config.yaml \
    2>&1 | sed -u 's/^/[client] /'
