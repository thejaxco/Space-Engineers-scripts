// Versión del Código: v1.6

// Nombres de los pistones y rotores
string rotorCabezalName = "Rotor CABEZAL";
string[] rotorPatasNames = { "RP PATA 1", "RP PATA 2", "RP PATA 3", "RP PATA 4" };
string[] pistonesPatasNames = { "PP 1", "PP 2", "PP 3", "PP 4" };
string pistonPrincipalName = "PRINCIPAL";
string pinzaInfName = "PINZA INF";
string pinzaSupName = "PINZA SUP";
string panelControlName = "LCD"; // Nombre del panel de control
string panelEstadoName = "LCD2"; // Nombre del panel de estado

// Límites de grados para el rotor cabezal (rangos de activación de cada pistón)
float[] lowerLimitsDeg = { 180f, 90f, 0f, 270f };    // Límites inferiores para cada pata
float[] upperLimitsDeg = { 210f, 120f, 30f, 300f };   // Límites superiores para cada pata

// Rango de altura del pistón principal para detectar el obstáculo
float obstacleHeight = 5f;   // La altura "y" en la que aparece el obstáculo
float currentHeight = 0f;     // La altura actual del pistón principal

// Variables de estado
bool extenderPiston = true;
int ciclosCompletados = 0;
int profundidad = 0; // Variable para guardar el valor máximo de los ciclos de bajada completados
bool bajar = true; // Variable para determinar si se baja o se sube
bool cicloCompletado = false; // Variable para controlar si el ciclo se ha completado

// Estados iniciales
Dictionary<string, float> estadosIniciales = new Dictionary<string, float>();

// Lista para almacenar los mensajes de registro
List<string> log = new List<string>();
List<string> estadoLog = new List<string>();

void Main(string argument)
{
    // Borrar el texto existente en los paneles de control
    BorrarPantalla(panelControlName);
    BorrarPantalla(panelEstadoName);

    log.Add($"{DateTime.Now}: Versión del Código: v1.6");
    log.Add($"{DateTime.Now}: Argumento recibido: {argument}");

    // Determinar la acción basada en el argumento
    if (string.IsNullOrEmpty(argument))
    {
        log.Add($"{DateTime.Now}: Argumento no válido. Use 'bajar' o 'subir'.");
        Echo("Argumento no válido. Use 'bajar' o 'subir'.");
        MostrarLog(panelControlName, log);
        return;
    }

    if (argument == "bajar")
    {
        bajar = true;
        cicloCompletado = false;
        log.Add($"{DateTime.Now}: Iniciando ciclo de bajada.");
        IniciarCiclo();
    }
    else if (argument == "subir")
    {
        bajar = false;
        cicloCompletado = false;
        log.Add($"{DateTime.Now}: Iniciando ciclo de subida.");
        IniciarCiclo();
    }
    else
    {
        log.Add($"{DateTime.Now}: Argumento no válido. Use 'bajar' o 'subir'.");
        Echo("Argumento no válido. Use 'bajar' o 'subir'.");
        MostrarLog(panelControlName, log);
        return;
    }

    // Mostrar el log en el panel de control
    MostrarLog(panelControlName, log);

    // Actualizar la frecuencia para que el script se ejecute en intervalos
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

void IniciarCiclo()
{
    // Obtener los bloques
    IMyMotorStator rotorCabezal = GridTerminalSystem.GetBlockWithName(rotorCabezalName) as IMyMotorStator;
    IMyPistonBase pistonPrincipal = GridTerminalSystem.GetBlockWithName(pistonPrincipalName) as IMyPistonBase;
    IMyMechanicalConnectionBlock pinzaInf = GridTerminalSystem.GetBlockWithName(pinzaInfName) as IMyMechanicalConnectionBlock;
    IMyMechanicalConnectionBlock pinzaSup = GridTerminalSystem.GetBlockWithName(pinzaSupName) as IMyMechanicalConnectionBlock;

    if (rotorCabezal == null || pistonPrincipal == null || pinzaInf == null || pinzaSup == null)
    {
        log.Add($"{DateTime.Now}: Uno o más bloques no encontrados.");
        MostrarLog(panelControlName, log);
        return;
    }

    // Verificar el estado inicial de las pinzas y la altura del pistón principal
    log.Add($"{DateTime.Now}: Estado inicial de PINZA INF: {(pinzaInf.IsAttached ? "Unida" : "Desunida")}");
    log.Add($"{DateTime.Now}: Estado inicial de PINZA SUP: {(pinzaSup.IsAttached ? "Unida" : "Desunida")}");
    currentHeight = pistonPrincipal.CurrentPosition;
    log.Add($"{DateTime.Now}: Altura inicial del pistón principal: {currentHeight}");

    // Obtener los pistones de las patas
    List<IMyPistonBase> pistonesPatas = new List<IMyPistonBase>();
    List<IMyMotorStator> rotoresPatas = new List<IMyMotorStator>();
    foreach (string pistónName in pistonesPatasNames)
    {
        IMyPistonBase pistón = GridTerminalSystem.GetBlockWithName(pistónName) as IMyPistonBase;
        if (pistón != null)
        {
            pistonesPatas.Add(pistón);
            log.Add($"{DateTime.Now}: Estado inicial del pistón {pistónName}: Posición = {pistón.CurrentPosition}, Velocidad = {pistón.Velocity}");
        }
        else
        {
            log.Add($"{DateTime.Now}: No se encontró el pistón: {pistónName}");
        }
    }
    foreach (string rotorName in rotorPatasNames)
    {
        IMyMotorStator rotor = GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorStator;
        if (rotor != null)
        {
            rotoresPatas.Add(rotor);
            log.Add($"{DateTime.Now}: Estado inicial del rotor {rotorName}: Ángulo = {rotor.Angle * 180f / (float)Math.PI}, Velocidad = {rotor.TargetVelocityRPM}");
        }
        else
        {
            log.Add($"{DateTime.Now}: No se encontró el rotor: {rotorName}");
        }
    }

    // Guardar estados iniciales
    estadosIniciales[rotorCabezalName] = rotorCabezal.Angle;
    estadosIniciales[pistonPrincipalName] = pistonPrincipal.CurrentPosition;
    foreach (var pistón in pistonesPatas)
    {
        estadosIniciales[pistón.CustomName] = pistón.CurrentPosition;
    }
    foreach (var rotor in rotoresPatas)
    {
        estadosIniciales[rotor.CustomName] = rotor.Angle;
    }

    // Iniciar ciclo
    log.Add($"{DateTime.Now}: Ciclo iniciado.");
    extenderPiston = bajar;
    if (bajar)
    {
        Bajar(pinzaInf, pinzaSup, pistonPrincipal);
    }
    else
    {
        Subir(pinzaInf, pinzaSup, pistonPrincipal);
    }
}

void Bajar(IMyMechanicalConnectionBlock pinzaInf, IMyMechanicalConnectionBlock pinzaSup, IMyPistonBase pistonPrincipal)
{
    // Comprobar que PINZA SUP está unida antes de desunir PINZA INF
    if (!pinzaSup.IsAttached)
    {
        log.Add($"{DateTime.Now}: PINZA SUP no está unida. No se puede desunir PINZA INF.");
        MostrarLog(panelControlName, log);
        return;
    }

    // Desunir PINZA INF
    pinzaInf.Detach();
    log.Add($"{DateTime.Now}: Desuniendo PINZA INF.");

    // Detectar la altura del pistón principal
    currentHeight = pistonPrincipal.CurrentPosition;
    log.Add($"{DateTime.Now}: Altura actual del pistón principal: {currentHeight}");

    // Extender el pistón principal
    if (extenderPiston && !pinzaInf.IsAttached)
    {
        pistonPrincipal.Velocity = 1.0f;  // Extender el pistón principal
        log.Add($"{DateTime.Now}: Extiendo el pistón principal.");

        // Caso 1: No hay obstáculo, el pistón principal está extendido más del 5%
        if (currentHeight > obstacleHeight)
        {
            log.Add($"{DateTime.Now}: No hay obstáculo. Pistón principal extendido.");
            ControlarPatas();
        }

        // Unir PINZA INF cuando el pistón principal esté extendido al 73.5%
        if (currentHeight >= 7.35f)
        {
            pinzaInf.Attach();
            log.Add($"{DateTime.Now}: Uniendo PINZA INF.");
            extenderPiston = false;
        }
    }
    else if (!extenderPiston && pinzaInf.IsAttached && pinzaSup.IsAttached)
    {
        // Comprobar que PINZA INF está unida antes de desunir PINZA SUP
        if (!pinzaInf.IsAttached)
        {
            log.Add($"{DateTime.Now}: PINZA INF no está unida. No se puede desunir PINZA SUP.");
            MostrarLog(panelControlName, log);
            return;
        }

        pinzaSup.Detach();
        log.Add($"{DateTime.Now}: Desuniendo PINZA SUP.");
        pistonPrincipal.Velocity = -1.0f;  // Encoger el pistón principal
        log.Add($"{DateTime.Now}: Encogiendo el pistón principal.");

        // Caso 2: Hay un obstáculo, el pistón principal está encogido al 5% o menos
        if (currentHeight <= obstacleHeight)
        {
            log.Add($"{DateTime.Now}: Obstáculo detectado. Pistón principal encogido.");
            ControlarPatas();
        }

        // Unir PINZA SUP cuando el pistón principal esté encogido al 0%
        if (currentHeight <= 0f)
        {
            pinzaSup.Attach();
            log.Add($"{DateTime.Now}: Uniendo PINZA SUP.");
            extenderPiston = true;
            ciclosCompletados++;
            profundidad = Math.Max(profundidad, ciclosCompletados);
            log.Add($"{DateTime.Now}: Ciclo completado. Total ciclos: {ciclosCompletados}, Profundidad: {profundidad}");
            cicloCompletado = true;
        }
    }

    // Mostrar el log en el panel de control
    MostrarLog(panelControlName, log);
    MostrarEstado();
}

void Subir(IMyMechanicalConnectionBlock pinzaInf, IMyMechanicalConnectionBlock pinzaSup, IMyPistonBase pistonPrincipal)
{
    // Comprobar que PINZA INF está unida antes de desunir PINZA SUP
    if (!pinzaInf.IsAttached)
    {
        log.Add($"{DateTime.Now}: PINZA INF no está unida. No se puede desunir PINZA SUP.");
        MostrarLog(panelControlName, log);
        return;
    }

    // Desunir PINZA SUP
    pinzaSup.Detach();
    log.Add($"{DateTime.Now}: Desuniendo PINZA SUP.");

    // Detectar la altura del pistón principal
    currentHeight = pistonPrincipal.CurrentPosition;
    log.Add($"{DateTime.Now}: Altura actual del pistón principal: {currentHeight}");

    // Extender el pistón principal
    if (extenderPiston && !pinzaSup.IsAttached)
    {
        pistonPrincipal.Velocity = 1.0f;  // Extender el pistón principal
        log.Add($"{DateTime.Now}: Extiendo el pistón principal.");

        // Caso 1: No hay obstáculo, el pistón principal está extendido más del 5%
        if (currentHeight > obstacleHeight)
        {
            log.Add($"{DateTime.Now}: No hay obstáculo. Pistón principal extendido.");
            ControlarPatas();
        }

        // Unir PINZA SUP cuando el pistón principal esté extendido al 73.5%
        if (currentHeight >= 7.35f)
        {
            pinzaSup.Attach();
            log.Add($"{DateTime.Now}: Uniendo PINZA SUP.");
            extenderPiston = false;
        }
    }
    else if (!extenderPiston && pinzaInf.IsAttached && pinzaSup.IsAttached)
    {
        // Comprobar que PINZA SUP está unida antes de desunir PINZA INF
        if (!pinzaSup.IsAttached)
        {
            log.Add($"{DateTime.Now}: PINZA SUP no está unida. No se puede desunir PINZA INF.");
            MostrarLog(panelControlName, log);
            return;
        }

        pinzaInf.Detach();
        log.Add($"{DateTime.Now}: Desuniendo PINZA INF.");
        pistonPrincipal.Velocity = -1.0f;  // Encoger el pistón principal
        log.Add($"{DateTime.Now}: Encogiendo el pistón principal.");

        // Caso 2: Hay un obstáculo, el pistón principal está encogido al 5% o menos
        if (currentHeight <= obstacleHeight)
        {
            log.Add($"{DateTime.Now}: Obstáculo detectado. Pistón principal encogido.");
            ControlarPatas();
        }

        // Unir PINZA INF cuando el pistón principal esté encogido al 0%
        if (currentHeight <= 0f)
        {
            pinzaInf.Attach();
            log.Add($"{DateTime.Now}: Uniendo PINZA INF.");
            extenderPiston = true;
            ciclosCompletados--;
            log.Add($"{DateTime.Now}: Ciclo completado. Total ciclos: {ciclosCompletados}, Profundidad: {profundidad}");
            cicloCompletado = true;
        }
    }

    // Mostrar el log en el panel de control
    MostrarLog(panelControlName, log);
    MostrarEstado();
}

void ControlarPatas()
{
    // Obtener los bloques
    IMyMotorStator rotorCabezal = GridTerminalSystem.GetBlockWithName(rotorCabezalName) as IMyMotorStator;
    List<IMyPistonBase> pistonesPatas = new List<IMyPistonBase>();
    List<IMyMotorStator> rotoresPatas = new List<IMyMotorStator>();
    foreach (string pistónName in pistonesPatasNames)
    {
        IMyPistonBase pistón = GridTerminalSystem.GetBlockWithName(pistónName) as IMyPistonBase;
        if (pistón != null)
        {
            pistonesPatas.Add(pistón);
        }
    }
    foreach (string rotorName in rotorPatasNames)
    {
        IMyMotorStator rotor = GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorStator;
        if (rotor != null)
        {
            rotoresPatas.Add(rotor);
        }
    }

    // Comprobar la posición del Rotor CABEZAL
    float rotorAngle = rotorCabezal.Angle * 180f / (float)Math.PI;  // Convertimos el ángulo de radianes a grados
    log.Add($"{DateTime.Now}: Ángulo actual del Rotor CABEZAL: {rotorAngle}");

    for (int i = 0; i < rotorPatasNames.Length; i++)
    {
        // Calcular el ángulo de la pata en función del ángulo del rotor cabezal
        float pataAngle = (rotorAngle + lowerLimitsDeg[i]) % 360;

        // Si el ángulo de la pata está dentro del rango de 180º a 210º, hacemos el movimiento pendular
        if (pataAngle >= 180f && pataAngle <= 210f)
        {
            PendulumMovement(rotoresPatas[i]);
            log.Add($"{DateTime.Now}: Movimiento pendular del rotor {rotorPatasNames[i]}.");
        }
        else
        {
            // Si no está dentro del rango de la pata, retraemos el pistón
            RetractPiston(pistonesPatas[i]);
            log.Add($"{DateTime.Now}: Retrayendo pistón {pistonesPatasNames[i]}.");
        }
    }

    // Mostrar el log en el panel de control
    MostrarLog(panelControlName, log);
    MostrarEstado();
}

// Función para extender pistones
void ExtendPiston(IMyPistonBase piston)
{
    piston.MaxLimit = 10.0f;  // Establecer el límite máximo del pistón
    piston.MinLimit = 0.0f;   // Establecer el límite mínimo del pistón
    piston.Velocity = 1.0f;   // Establecer la velocidad de extensión
}

// Función para retraer pistones
void RetractPiston(IMyPistonBase piston)
{
    piston.MaxLimit = 0.0f;   // Establecer el límite máximo del pistón
    piston.MinLimit = 0.0f;   // Establecer el límite mínimo del pistón
    piston.Velocity = -1.0f;  // Establecer la velocidad de retracción
}

// Función para hacer el movimiento pendular del rotor
void PendulumMovement(IMyMotorStator rotor)
{
    float currentAngle = rotor.Angle * 180f / (float)Math.PI;
    if (currentAngle < 260f)
    {
        rotor.TargetVelocityRPM = 1f;  // Mover hacia 290º
    }
    else if (currentAngle > 290f)
    {
        rotor.TargetVelocityRPM = -1f; // Mover hacia 260º
    }
}

// Función para mover un rotor hacia 0º
void MoveRotorTo0(IMyMotorStator rotor)
{
    float currentAngle = rotor.Angle * 180f / (float)Math.PI;
    if (currentAngle < 0f)
    {
        rotor.TargetVelocityRPM = 1f;  // Mover hacia 0º
    }
    else if (currentAngle > 0f)
    {
        rotor.TargetVelocityRPM = -1f; // Mover hacia 0º
    }
    else
    {
        rotor.TargetVelocityRPM = 0f;  // Detener el rotor si ya está en 0º
    }
}

// Función para mostrar el log en el panel de control
void MostrarLog(string panelName, List<string> log)
{
    IMyTextPanel panel = GridTerminalSystem.GetBlockWithName(panelName) as IMyTextPanel;
    if (panel != null)
    {
        panel.ContentType = ContentType.TEXT_AND_IMAGE; // Asegurarse de que el panel esté en el modo correcto
        panel.WriteText(string.Join("\n", log));
    }
    else
    {
        Echo("Panel de control no encontrado.");
    }
}

// Función para borrar el texto en el panel de control
void BorrarPantalla(string panelName)
{
    IMyTextPanel panel = GridTerminalSystem.GetBlockWithName(panelName) as IMyTextPanel;
    if (panel != null)
    {
        panel.ContentType = ContentType.TEXT_AND_IMAGE; // Asegurarse de que el panel esté en el modo correcto
        panel.WriteText(string.Empty); // Borrar el texto existente
    }
    else
    {
        Echo("Panel de control no encontrado.");
    }
}

// Función para mostrar el estado en el panel de estado
void MostrarEstado()
{
    estadoLog.Clear();
    estadoLog.Add($"Ciclos Completados: {ciclosCompletados}");
    estadoLog.Add($"Profundidad: {profundidad}");
    estadoLog.Add($"Altura Actual: {currentHeight}");
    estadoLog.Add($"Extender Piston: {extenderPiston}");
    estadoLog.Add($"Bajar: {bajar}");
    estadoLog.Add($"Ciclo Completado: {cicloCompletado}");
    MostrarLog(panelEstadoName, estadoLog);
}
