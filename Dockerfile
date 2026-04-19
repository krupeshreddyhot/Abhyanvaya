# Build from repository root (folder containing Abhyanvaya.API/).
# Example: docker build -t abhyanvaya-api .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Abhyanvaya.Domain/Abhyanvaya.Domain.csproj Abhyanvaya.Domain/
COPY Abhyanvaya.Application/Abhyanvaya.Application.csproj Abhyanvaya.Application/
COPY Abhyanvaya.Infrastructure/Abhyanvaya.Infrastructure.csproj Abhyanvaya.Infrastructure/
COPY Abhyanvaya.API/Abhyanvaya.API.csproj Abhyanvaya.API/

RUN dotnet restore Abhyanvaya.API/Abhyanvaya.API.csproj

COPY Abhyanvaya.Domain/ Abhyanvaya.Domain/
COPY Abhyanvaya.Application/ Abhyanvaya.Application/
COPY Abhyanvaya.Infrastructure/ Abhyanvaya.Infrastructure/
COPY Abhyanvaya.API/ Abhyanvaya.API/

WORKDIR /src/Abhyanvaya.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Abhyanvaya.API.dll"]
