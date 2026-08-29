FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY WrenchWorks.sln .
COPY src/WrenchWorks.Domain/WrenchWorks.Domain.csproj src/WrenchWorks.Domain/
COPY src/WrenchWorks.Infrastructure/WrenchWorks.Infrastructure.csproj src/WrenchWorks.Infrastructure/
COPY src/WrenchWorks.Api/WrenchWorks.Api.csproj src/WrenchWorks.Api/
RUN dotnet restore

COPY . .
RUN dotnet publish src/WrenchWorks.Api/WrenchWorks.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WrenchWorks.Api.dll"]
