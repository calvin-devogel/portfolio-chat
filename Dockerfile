FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

ARG TARGET_ARCH=x64

COPY nuget.config .
COPY portfolio-chat.csproj .
RUN dotnet restore -r linux-musl-${TARGET_ARCH}

COPY . .
RUN dotnet publish portfolio-chat.csproj -c Release -o /app/publish \
    -r linux-musl-${TARGET_ARCH} \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:InvariantGlobalization=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime
WORKDIR /app

RUN apk update && apk add --no-cache ca-certificates
RUN adduser -D appuser
USER appuser

COPY --from=build /app/publish/portfolio-chat .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./portfolio-chat"]