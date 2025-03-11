using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Clase principal para el control del movimiento del jugador.
// Este script gestiona el movimiento, salto, dash, estamina, vida, dano de caida y curacion.
public class PlayerController : MonoBehaviour
{
    // -------------------------
    // CONFIGURACION DE MOVIMIENTO
    // -------------------------
    [Header("Movimiento")]
    public float speed = 5f;                    // Velocidad base del movimiento
    public float sprintMultiplier = 1.5f;       // Multiplicador de velocidad al sprintar
    public float rotationSpeed = 720f;          // Velocidad de rotacion del jugador

    // -------------------------
    // CONFIGURACION DE SALTO Y GRAVEDAD
    // -------------------------
    [Header("Salto y Gravedad")]
    public float jumpForce = 5f;                // Fuerza del salto
    public float gravity = -20f;                // Valor de la gravedad
    public float jumpBufferDuration = 0.2f;     // Duracion del buffer de salto

    // -------------------------
    // CONFIGURACION DEL DASH / VOLTERETA
    // -------------------------
    [Header("Dash / Voltereta")]
    public float dashSpeed = 20f;               // Velocidad durante el dash
    public float dashDuration = 0.2f;           // Duracion del dash en segundos
    public float doubleTapThreshold = 0.3f;     // Tiempo maximo entre dos pulsaciones de Shift para considerarlo doble toque

    // -------------------------
    // CONFIGURACION DE ESTAMINA
    // -------------------------
    [Header("Estamina")]
    public float maxStamina = 100f;                     // Estamina maxima
    public float staminaRecoveryRate = 10f;             // Puntos de estamina recuperados por segundo
    public float sprintStaminaCostPerSecond = 15f;      // Costo por segundo al sprintar
    public float dashStaminaCost = 30f;                 // Costo fijo para dash
    [Tooltip("Barra de estamina (UI Image con modo Fill)")]
    public Image staminaBar;                          // Referencia a la imagen de la barra de estamina

    // -------------------------
    // CONFIGURACION DE VIDA
    // -------------------------
    [Header("Vida")]
    public float maxHealth = 100f;                      // Vida maxima
    [Tooltip("Barra de vida (UI Image con modo Fill)")]
    public Image healthBar;                           // Referencia a la imagen de la barra de vida
    [Tooltip("Umbral de velocidad para sufrir dano de caida")]
    public float fallDamageThreshold = 10f;           // Velocidad minima (negativa) para que se aplique dano
    [Tooltip("Multiplicador para calcular el dano de caida")]
    public float fallDamageMultiplier = 2f;           // Multiplicador para calcular el dano de caida
    [Tooltip("Altura minima para que se aplique dano de caida")]
    public float minFallHeight = 2f;                  // Altura minima para que se aplique dano de caida

    // -------------------------
    // CONFIGURACION DE CURACION
    // -------------------------
    [Header("Curacion")]
    public float healDelay = 5f;                      // Tiempo minimo sin dano para comenzar a curarse
    public float healRate = 2f;                       // Cantidad de vida recuperada por segundo

    // -------------------------
    // CONFIGURACION DE MATERIALES
    // -------------------------
    [Header("Materiales")]
    [Tooltip("Material para cuando el jugador esta vivo")]
    public Material materialVivo;                    // Material asignado cuando el jugador esta vivo
    [Tooltip("Material para cuando el jugador esta muerto")]
    public Material materialMuerto;                  // Material asignado cuando el jugador esta muerto

    // -------------------------
    // COMPONENTES Y VARIABLES INTERNAS
    // -------------------------
    public CharacterController controller;          // Componente CharacterController del jugador

    private float verticalVelocity = 0f;             // Velocidad vertical actual
    private float jumpBufferTimer = 0f;              // Temporizador del buffer de salto
    private Transform camTransform;                  // Transform de la camara principal

    // Variables para la gestion del dash y sprint con Shift
    private bool waitingForSecondShiftTap = false;   // Indica si se esta esperando un segundo toque de Shift
    private float firstShiftTapTime = 0f;            // Tiempo del primer toque de Shift
    private bool isDashing = false;                  // Indica si se esta realizando un dash
    private float dashTimer = 0f;                    // Temporizador del dash
    private Vector3 dashDirection = Vector3.zero;    // Direccion del dash

    // Variables internas para la estamina
    private float currentStamina;                    // Estamina actual

    // Variables para la vida y el dano de caida
    private float currentHealth;                     // Vida actual
    private float maxFallVelocity = 0f;              // Registra la mayor velocidad negativa durante la caida
    private bool wasGrounded = true;                 // Estado del suelo en el frame anterior

    // Variable para registrar el tiempo del ultimo dano recibido
    private float lastDamageTime = 0f;               // Tiempo del ultimo dano

    // -------------------------
    // METODO START
    // Inicializa variables y configura el jugador
    // -------------------------
    void Start()
    {
        // Si no se asigno el CharacterController, se obtiene del objeto
        if (controller == null)
            controller = GetComponent<CharacterController>();

        // Se obtiene el transform de la camara principal, si existe
        if (Camera.main != null)
            camTransform = Camera.main.transform;

        // Se bloquea y oculta el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Se inicializan la estamina y la vida con sus valores maximos
        currentStamina = maxStamina;
        ActualizarBarraEstamina();

        currentHealth = maxHealth;
        ActualizarBarraVida();

        // Se asigna el material "Vivo" al inicio al PlayerCapsule
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && materialVivo != null)
        {
            renderer.material = materialVivo;
        }

        // Se inicializa lastDamageTime
        lastDamageTime = Time.time;
    }

    // -------------------------
    // METODO UPDATE
    // Se ejecuta cada frame para gestionar entrada, movimiento, salto, dash, estamina, caida, dano y curacion
    // -------------------------
    void Update()
    {
        // Si el jugador esta muerto, no se procesa nada
        if (currentHealth <= 0) return;

        // --- Actualizamos el estado del doble toque de Shift ---
        if (waitingForSecondShiftTap)
        {
            if (Time.time - firstShiftTapTime > doubleTapThreshold)
            {
                waitingForSecondShiftTap = false;
            }
        }

        // -------------------------
        // GESTION DEL SALTO CON BUFFER
        // -------------------------
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferDuration;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0, jumpBufferTimer - Time.deltaTime);
        }

        // Gestion de salto y gravedad:
        // Si el controlador esta en el suelo, se resetea la velocidad vertical
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = 0f;

            // Si hay salto en el buffer, se calcula la velocidad del salto
            if (jumpBufferTimer > 0)
            {
                verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
                jumpBufferTimer = 0f;
            }
        }
        else
        {
            // Si no esta en el suelo, se acumula la gravedad
            verticalVelocity += gravity * Time.deltaTime;
        }

        // -------------------------
        // OBTENCION DE ENTRADAS DE MOVIMIENTO
        // -------------------------
        float horizontal = 0f;
        float vertical = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) horizontal = -1f;
            if (Keyboard.current.dKey.isPressed) horizontal = 1f;
            if (Keyboard.current.sKey.isPressed) vertical = -1f;
            if (Keyboard.current.wKey.isPressed) vertical = 1f;
        }

        // Calculo del movimiento relativo a la camara
        Vector3 move = Vector3.zero;
        if (camTransform != null)
        {
            Vector3 camForward = camTransform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = camTransform.right;
            camRight.y = 0;
            camRight.Normalize();

            move = (camForward * vertical + camRight * horizontal);
        }
        else
        {
            move = new Vector3(horizontal, 0f, vertical);
        }

        // -------------------------
        // DETECCION DE SHIFT (DASH vs SPRINT)
        // -------------------------
        if (Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame && controller.isGrounded)
        {
            // Si no se esta esperando un segundo toque, se inicia la espera
            if (!waitingForSecondShiftTap)
            {
                waitingForSecondShiftTap = true;
                firstShiftTapTime = Time.time;
            }
            else
            {
                // Si el segundo toque se produce dentro del umbral, se activa el dash
                if (Time.time - firstShiftTapTime <= doubleTapThreshold)
                {
                    if (currentStamina >= dashStaminaCost)
                    {
                        isDashing = true;
                        dashTimer = dashDuration;
                        dashDirection = (move.magnitude > 0.1f) ? move.normalized : transform.forward;
                        currentStamina -= dashStaminaCost;
                        // Actualizar el tiempo del ultimo dano ya que se consume estamina
                        lastDamageTime = Time.time;
                    }
                    waitingForSecondShiftTap = false;
                }
            }
        }

        // -------------------------
        // APLICACION DEL DASH O MOVIMIENTO NORMAL CON SPRINT
        // -------------------------
        float currentSpeed = speed;
        if (!isDashing)
        {
            // Si se mantiene Shift, se aplica el sprint y se consume estamina
            if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
            {
                float staminaCost = sprintStaminaCostPerSecond * Time.deltaTime;
                if (currentStamina >= staminaCost)
                {
                    currentStamina -= staminaCost;
                    currentSpeed *= sprintMultiplier;
                }
            }

            // Si hay movimiento, se rota el jugador hacia la direccion del movimiento
            if (move.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                move = move.normalized * currentSpeed;
            }
            else
            {
                move = Vector3.zero;
            }
        }
        else
        {
            // Durante el dash se ignora el sprint y el movimiento normal
            move = dashDirection * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }

        // -------------------------
        // RECUPERACION DE ESTAMINA
        // -------------------------
        // Si no se esta sprintando o dash, se recupera estamina
        if (!(Keyboard.current != null && Keyboard.current.shiftKey.isPressed && !isDashing))
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        ActualizarBarraEstamina();

        // -------------------------
        // SE AGREGA LA COMPONENTE VERTICAL (SALTO Y GRAVEDAD)
        // -------------------------
        move += new Vector3(0f, verticalVelocity, 0f);

        // -------------------------
        // MOVIMIENTO DEL JUGADOR
        // -------------------------
        controller.Move(move * Time.deltaTime);

        // -------------------------
        // GESTION DEL DANO DE CAIDA
        // -------------------------
        if (!controller.isGrounded)
        {
            // Mientras se esta cayendo, se registra la velocidad mas negativa alcanzada
            maxFallVelocity = Mathf.Min(maxFallVelocity, verticalVelocity);
        }
        else
        {
            // Si acaba de aterrizar (cambio de estar en el aire a estar en el suelo)
            if (!wasGrounded)
            {
                if (maxFallVelocity < -fallDamageThreshold)
                {
                    // Dano de altura basado en velocidad
                    //float damage = Mathf.Pow(Mathf.Abs(maxFallVelocity) - fallDamageThreshold, 2) * fallDamageMultiplier;

                    // Calcular la altura total de la caida (segun la velocidad maxima alcanzada)
                    float height = Mathf.Pow(Mathf.Abs(maxFallVelocity), 2) / (2 * Mathf.Abs(gravity));

                    // Solo aplicar dano si la altura total supera la altura minima
                    if (height >= minFallHeight)
                    {
                        // Calcular la altura "efectiva" de la caida (por encima del minimo)
                        float effectiveHeight = height - minFallHeight;
                        // Si effectiveHeight es 0 o negativo, no se aplica dano
                        if (effectiveHeight > 0)
                        {
                            // Calcular el dano de manera exponencial basado en la altura efectiva
                            float damage = Mathf.Pow(effectiveHeight, 1.5f) * fallDamageMultiplier;
                            currentHealth -= damage;
                            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                            ActualizarBarraVida();

                            // Actualizar el tiempo del ultimo dano
                            lastDamageTime = Time.time;

                            // Llamar a Die() si la vida llega a 0
                            if (currentHealth <= 0)
                            {
                                Die();
                            }
                        }
                    }
                }
                maxFallVelocity = 0f;
            }
        }
        wasGrounded = controller.isGrounded;

        // -------------------------
        // CURACION AUTOMATICA
        // -------------------------
        // Si ha pasado el tiempo de espera sin dano, el jugador se cura poco a poco
        if (currentHealth < maxHealth && (Time.time - lastDamageTime) >= healDelay)
        {
            currentHealth += healRate * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            ActualizarBarraVida();
        }
    }

    // -------------------------
    // FUNCION DE MUERTE
    // Cuando la vida llegue a 0, se ejecuta esta funcion
    // -------------------------
    void Die()
    {
        Debug.Log("El jugador ha muerto");

        // Cambiar el material del PlayerCapsule a "Muerto"
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && materialMuerto != null)
        {
            renderer.material = materialMuerto;
        }

        // Desactivar el control del jugador
        this.enabled = false;

        // Opcional: Activar pantalla de "Game Over"
        //GameManager.Instance.ShowGameOverScreen();

        // Opcional: Reiniciar la escena tras 2 segundos
        Invoke("ReiniciarEscena", 2f);
    }

    // -------------------------
    // FUNCION PARA REINICIAR LA ESCENA
    // Reinicia la escena actual (opcional)
    // -------------------------
    void ReiniciarEscena()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // -------------------------
    // FUNCION PARA ACTUALIZAR LA BARRA DE ESTAMINA
    // -------------------------
    void ActualizarBarraEstamina()
    {
        if (staminaBar != null)
        {
            staminaBar.fillAmount = currentStamina / maxStamina;
        }
    }

    // -------------------------
    // FUNCION PARA ACTUALIZAR LA BARRA DE VIDA
    // -------------------------
    void ActualizarBarraVida()
    {
        if (healthBar != null)
        {
            healthBar.fillAmount = currentHealth / maxHealth;
        }
    }
}
