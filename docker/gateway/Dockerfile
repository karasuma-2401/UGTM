# Rebuild the upstream Caddy release with patched Go and x/net until the
# upstream runtime image includes those fixes. Keep its runtime/data layout.
FROM golang:1.27.4-alpine@sha256:70bd7ddea8b6cbe5d6943cde279d6b67feb10cc924fb9100d1d8757c1575b280 AS build
WORKDIR /src
RUN go mod init ugtm-gateway \
    && go get github.com/caddyserver/caddy/v2@v2.11.9 golang.org/x/net@v0.62.0
COPY main.go .
RUN go mod tidy \
    && CGO_ENABLED=0 go build -trimpath -ldflags='-s -w' -o /out/caddy .

FROM caddy:2.11.9-alpine@sha256:003a97808e781aa9f7e75cc2f43d34d461099031faf5e20faefb9f61126836f0
COPY --from=build /out/caddy /usr/bin/caddy
