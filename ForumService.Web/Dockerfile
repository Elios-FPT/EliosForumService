# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ForumService.csproj", "."]
RUN dotnet restore "ForumService.csproj"

# Copy the rest of the code and build
COPY . .
RUN dotnet build "ForumService.csproj" -c Release -o /app/build

# Publish the app
RUN dotnet publish "ForumService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "ForumService.dll"]