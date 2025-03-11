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
    public float dashDuration = 0.2f;         // Duración del dash en segundos
    public float doubleTapThreshold = 0.3f;   // Tiempo máximo entre dos pulsaciones de Shift para considerarlo doble toque

    [Header("Estamina")]
    public float maxStamina = 100f;
    public float staminaRecoveryRate = 10f;         // Puntos de estamina recuperados por segundo
    public float sprintStaminaCostPerSecond = 15f;    // Costo por segundo al sprintar
    public float dashStaminaCost = 30f;               // Costo fijo para dash
    [Tooltip("Barra de estamina (UI Image con modo Fill)")]
    public Image staminaBar;                        // Asigna el componente Image de la barra de estamina

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

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (Camera.main != null)
            camTransform = Camera.main.transform;

        // Bloquear y ocultar el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Inicializar estamina
        currentStamina = maxStamina;
        ActualizarBarraEstamina();
    }

    void Update()
    {
        // --- Actualizamos el estado de doble toque ---
        if (waitingForSecondShiftTap)
        {
            // Si se supera el umbral sin recibir el segundo toque, cancelamos la espera
            if (Time.time - firstShiftTapTime > doubleTapThreshold)
            {
                waitingForSecondShiftTap = false;
            }
        }

        // -------------------------
        // GESTIÓN DEL SALTO CON BUFFER
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
        // OBTENCIÓN DE ENTRADAS DE MOVIMIENTO
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

        // Movimiento relativo a la cámara
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
        // DETECCIÓN DE SHIFT (DASH vs SPRINT)
        // -------------------------
        // Si se presiona Shift en este frame:
        if (Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame && controller.isGrounded)
        {
            // Si no se estaba esperando un segundo toque, iniciamos la espera
            if (!waitingForSecondShiftTap)
            {
                waitingForSecondShiftTap = true;
                firstShiftTapTime = Time.time;
            }
            else
            {
                // Si ya se estaba esperando y la pulsación ocurre dentro del umbral, se activa el dash
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
            // Si Shift está presionado y no se está esperando un doble toque (o la espera ya se canceló), se aplica sprint
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
            // Durante el dash, se ignora el sprint y el movimiento normal
            move = dashDirection * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }

        // -------------------------
        // RECUPERACIÓN DE ESTAMINA
        // -------------------------
        // Si no se está sprintando o dashando, se recupera la estamina
        if (!(Keyboard.current != null && Keyboard.current.shiftKey.isPressed && !isDashing))
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        ActualizarBarraEstamina();

        // -------------------------
        // AÑADIR LA COMPONENTE VERTICAL (SALTO Y GRAVEDAD)
        // -------------------------
        move += new Vector3(0f, verticalVelocity, 0f);

        // -------------------------
        // MOVER AL PERSONAJE
        // -------------------------
        controller.Move(move * Time.deltaTime);
    }

    void ActualizarBarraEstamina()
    {
        if (staminaBar != null)
        {
            staminaBar.fillAmount = currentStamina / maxStamina;
        }
    }
}
