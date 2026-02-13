#!/bin/sh
set -eu

CONTENT_DIR="${CONTENT_DIR:-/app/content}"
CONTENT_REPO="${CONTENT_REPO:-}"
CONTENT_REF="${CONTENT_REF:-main}"
echo "CONTENT_REPO=$CONTENT_REPO"
echo "CONTENT_REF=$CONTENT_REF"
echo "CONTENT_DIR=$CONTENT_DIR"

if [ -n "$CONTENT_REPO" ]; then
  if [ ! -d "$CONTENT_DIR/.git" ]; then
    rm -rf "$CONTENT_DIR"
    git clone --depth 1 --branch "$CONTENT_REF" "$CONTENT_REPO" "$CONTENT_DIR"
  else
    git -C "$CONTENT_DIR" fetch --depth 1 origin "$CONTENT_REF"
    git -C "$CONTENT_DIR" reset --hard "origin/$CONTENT_REF"
  fi
fi

exec dotnet Downpatch.Web.dll
