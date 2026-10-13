FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY DJQbox.sln ./
COPY src/DJQbox.Api/DJQbox.Api.csproj src/DJQbox.Api/
COPY src/DJQbox.Tests/DJQbox.Tests.csproj src/DJQbox.Tests/
RUN dotnet restore src/DJQbox.Api/DJQbox.Api.csproj

COPY src/DJQbox.Api/ src/DJQbox.Api/
WORKDIR /app/src/DJQbox.Api
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DJQbox.Api.dll"]
