   if btn_id == "c_videollamada":
        # Construye un nombre de sala único y corto
        room = f"ortho-{uuid.uuid4().hex[:8]}"
        # Intenta poner el nombre del paciente (si existe) para mostrarlo en Jitsi
        paciente = SESSION[user].get("paciente") or {}
        nombre_paciente = paciente.get("Paciente") or paciente.get("nombre") or f"CC {SESSION[user].get('dni','')}"
        # Opcional: título de la reunión
        subject = "Videollamada Ortopedia"

        link = generate_jitsi_link(room, display_name=nombre_paciente, subject=subject)

        SESSION[user]["step"] = "main_menu"
        # Enviar botón CTA
        wa_send_cta_url(user, "Abrir sala de videollamada segura.", link, "Unirme a la videollamada")
        # Enviar también el link en texto (respaldo)
        wa_send_text(user, f"🔗 Enlace directo: {link}")
        # Volver a mostrar menú principal
        wa_send_list_menu(user)
        return None

******************
********************
def generate_jitsi_link(room_slug: str, display_name: str = "", subject: str = "") -> str:
    """
    Genera un link listo de Jitsi (meet.jit.si) con nombre sugerido y título opcional.
    - room_slug: nombre único de la sala (sin espacios).
    - display_name: nombre que Jitsi intentará prellenar (el usuario puede editarlo).
    - subject: título de la reunión (opcional).

    Notas:
    - En meet.jit.si los parámetros de #config y #userInfo son best-effort.
    - No se puede preasignar contraseña vía URL en meet.jit.si público.
    """
    base = os.getenv("VIDEO_BASE_URL", "https://meet.jit.si").rstrip("/")
    # Parametrización útil (prejoin activado y nombre sugerido)
    fragments = []
    if subject:
        fragments.append(f"config.subject={quote(subject)}")
    # Mostrar pantalla de pre-join (útil para elegir mic/cam)
    fragments.append("config.prejoinConfig.enabled=true")
    if display_name:
        fragments.append(f"userInfo.displayName={quote(display_name)}")
    frag = "#" + "&".join(fragments) if fragments else ""
    return f"{base}/{room_slug}{frag}"






