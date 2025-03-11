using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;
    public float sprintMultiplier = 1.5f;
    public float rotationSpeed = 720f;

    [Header("Salto y Gravedad")]
    public float jumpForce = 5f;
    public float gravity = -20f;
    public float jumpBufferDuration = 0.2f;

    [Header("Dash / Voltereta")]
    public float dashSpeed = 20f;           // Velocidad durante el dash
    public float dashDuration = 0.2f;         // Duraci�n del dash en segundos
    public float doubleTapThreshold = 0.3f;   // Tiempo m�ximo entre dos pulsaciones de Shift para considerarlo doble toque

    [Header("Estamina")]
    public float maxStamina = 100f;
    public float staminaRecoveryRate = 10f;         // Puntos de estamina recuperados por segundo
    public float sprintStaminaCostPerSecond = 15f;    // Costo por segundo al sprintar
    public float dashStaminaCost = 30f;               // Costo fijo para dash
    [Tooltip("Barra de estamina (UI Image con modo Fill)")]
    public Image staminaBar;                        // Asigna el componente Image de la barra de estamina

    [Header("Vida")]
    public float maxHealth = 100f;
    [Tooltip("Barra de vida (UI Image con modo Fill)")]
    public Image healthBar;                         // Asigna el componente Image de la barra de vida
    [Tooltip("Umbral de velocidad para sufrir da�o de ca�da")]
    public float fallDamageThreshold = 10f;         // Velocidad m�nima (negativa) para que se aplique da�o
    [Tooltip("Multiplicador para calcular el da�o de ca�da")]
    public float fallDamageMultiplier = 2f;         // Cada punto por debajo del umbral se multiplica para calcular el da�o

    public CharacterController controller;

    private float verticalVelocity = 0f;
    private float jumpBufferTimer = 0f;
    private Transform camTransform;

    // Variables para el dash / sprint con Shift
    private bool waitingForSecondShiftTap = false;
    private float firstShiftTapTime = 0f;
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector3 dashDirection = Vector3.zero;

    // Estamina interna
    private float currentStamina;

    // Variables para la vida y ca�da
    private float currentHealth;
    private float maxFallVelocity = 0f;    // Registra la mayor velocidad negativa durante la ca�da
    private bool wasGrounded = true;       // Estado del suelo del frame anterior

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (Camera.main != null)
            camTransform = Camera.main.transform;

        // Bloquear y ocultar el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Inicializar estamina y vida
        currentStamina = maxStamina;
        ActualizarBarraEstamina();

        currentHealth = maxHealth;
        ActualizarBarraVida();
    }

    void Update()
    {
        // Si el jugador est� muerto, no hacer nada
        if (currentHealth <= 0) return;

        // --- Actualizamos el estado de doble toque ---
        if (waitingForSecondShiftTap)
        {
            if (Time.time - firstShiftTapTime > doubleTapThreshold)
            {
                waitingForSecondShiftTap = false;
            }
        }

        // -------------------------
        // GESTI�N DEL SALTO CON BUFFER
        // -------------------------
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferDuration;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0, jumpBufferTimer - Time.deltaTime);
        }

        // Salto y gravedad
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = 0f;

            if (jumpBufferTimer > 0)
            {
                verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
                jumpBufferTimer = 0f;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // -------------------------
        // OBTENCI�N DE ENTRADAS DE MOVIMIENTO
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

        // Movimiento relativo a la c�mara
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
        // DETECCI�N DE SHIFT (DASH vs SPRINT)
        // -------------------------
        if (Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame && controller.isGrounded)
        {
            if (!waitingForSecondShiftTap)
            {
                waitingForSecondShiftTap = true;
                firstShiftTapTime = Time.time;
            }
            else
            {
                if (Time.time - firstShiftTapTime <= doubleTapThreshold)
                {
                    if (currentStamina >= dashStaminaCost)
                    {
                        isDashing = true;
                        dashTimer = dashDuration;
                        dashDirection = (move.magnitude > 0.1f) ? move.normalized : transform.forward;
                        currentStamina -= dashStaminaCost;
                    }
                    waitingForSecondShiftTap = false;
                }
            }
        }

        // -------------------------
        // APLICAR DASH O MOVIMIENTO NORMAL CON SPRINT
        // -------------------------
        float currentSpeed = speed;
        if (!isDashing)
        {
            if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
            {
                float staminaCost = sprintStaminaCostPerSecond * Time.deltaTime;
                if (currentStamina >= staminaCost)
                {
                    currentStamina -= staminaCost;
                    currentSpeed *= sprintMultiplier;
                }
            }

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
            move = dashDirection * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }

        // -------------------------
        // RECUPERACI�N DE ESTAMINA
        // -------------------------
        if (!(Keyboard.current != null && Keyboard.current.shiftKey.isPressed && !isDashing))
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        ActualizarBarraEstamina();

        // -------------------------
        // A�ADIR LA COMPONENTE VERTICAL (SALTO Y GRAVEDAD)
        // -------------------------
        move += new Vector3(0f, verticalVelocity, 0f);

        // -------------------------
        // MOVER AL PERSONAJE
        // -------------------------
        controller.Move(move * Time.deltaTime);

        // -------------------------
        // GESTI�N DEL DA�O DE CA�DA
        // -------------------------
        if (!controller.isGrounded)
        {
            maxFallVelocity = Mathf.Min(maxFallVelocity, verticalVelocity);
        }
        else
        {
            if (!wasGrounded)
            {
                if (maxFallVelocity < -fallDamageThreshold)
                {
                    // Da�o de altura basado en velocidad
                    //float damage = Mathf.Pow(Mathf.Abs(maxFallVelocity) - fallDamageThreshold, 2) * fallDamageMultiplier;
                    //currentHealth -= damage;

                    float height = Mathf.Pow(Mathf.Abs(maxFallVelocity), 2) / (2 * Mathf.Abs(gravity));
                    float damage = Mathf.Pow(height, 1.5f) * fallDamageMultiplier;
                    currentHealth -= damage;
                    currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                    ActualizarBarraVida();

                    // Llamar a Die() si la vida llega a 0
                    if (currentHealth <= 0)
                    {
                        Die();
                    }
                }
                maxFallVelocity = 0f;
            }
        }
        wasGrounded = controller.isGrounded;
    }

    // -------------------------
    // FUNCI�N DE MUERTE
    // -------------------------
    void Die()
    {
        Debug.Log("El jugador ha muerto");

        // Desactivar control del jugador
        this.enabled = false;

        // Opcional: Activar pantalla de "Game Over"
        //GameManager.Instance.ShowGameOverScreen();

        // Opcional: Reiniciar la escena tras 2 segundos
        Invoke("ReiniciarEscena", 2f);
    }

    // Reinicia la escena (opcional)
    void ReiniciarEscena()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }


    void ActualizarBarraEstamina()
    {
        if (staminaBar != null)
        {
            staminaBar.fillAmount = currentStamina / maxStamina;
        }
    }

    void ActualizarBarraVida()
    {
        if (healthBar != null)
        {
            healthBar.fillAmount = currentHealth / maxHealth;
        }
    }
}
