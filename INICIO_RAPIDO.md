# Inicio Rapido

## Ejecutar el Proyecto

```powershell
dotnet run
```

La aplicacion estara disponible en: http://localhost:5000

## Configurar Webhook en Meta

1. Si estas en desarrollo local, descarga ngrok desde: https://ngrok.com/download

2. Ejecuta ngrok:
```powershell
ngrok http 5000
```

3. Copia la URL HTTPS generada por ngrok

4. Ve a https://developers.facebook.com/

5. Configurar webhook:
   - URL: https://tu-url-ngrok.io/api/webhook/whatsapp
   - Verify Token: (el que configuraste en appsettings.json)
   - Suscribirse a: messages

## Probar el Sistema

1. Desde WhatsApp, envia "Hola" al numero de prueba
2. Abre http://localhost:5000 en tu navegador
3. Ingresa tu nombre como operador
4. Veras la conversacion aparecer en el panel
