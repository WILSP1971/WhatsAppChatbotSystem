# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["WhatsAppChatbotSystem.csproj", "./"]
RUN dotnet restore "WhatsAppChatbotSystem.csproj"

# Copiar el resto de archivos y compilar
COPY . .
RUN dotnet build "WhatsAppChatbotSystem.csproj" -c Release -o /app/build
RUN dotnet publish "WhatsAppChatbotSystem.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copiar archivos publicados
COPY --from=build /app/publish .

# Exponer puerto (Render usa PORT variable de entorno)
EXPOSE 10000

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production

# Comando de inicio
ENTRYPOINT ["dotnet", "WhatsAppChatbotSystem.dll"]