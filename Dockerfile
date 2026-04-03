FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config .
COPY portfolio-chat.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish portfolio-chat.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# RUN adduser --disabled-pasword --no-create-home appuser
# USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "portfolio-chat.dll"]