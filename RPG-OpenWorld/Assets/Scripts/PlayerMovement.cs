using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 720f;
    public float jumpForce = 5f;
    public float gravity = -20f;
    public float jumpBufferDuration = 0.2f;

    public CharacterController controller;

    private float verticalVelocity = 0f;
    private float jumpBufferTimer = 0f;
    private Transform camTransform;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (Camera.main != null)
            camTransform = Camera.main.transform;

        // Bloquea el cursor en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
        // Oculta el cursor
        Cursor.visible = false;
    }

    void Update()
    {
        // Buffer para el salto
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

        // Obtener entradas horizontales y verticales
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
            // Obtener la dirección forward de la cámara, ignorando la componente Y
            Vector3 camForward = camTransform.forward;
            camForward.y = 0;
            camForward.Normalize();

            // Obtener la dirección right de la cámara
            Vector3 camRight = camTransform.right;
            camRight.y = 0;
            camRight.Normalize();

            // Combinar la entrada con las direcciones de la cámara
            move = (camForward * vertical + camRight * horizontal);
        }
        else
        {
            // Si no se encuentra la cámara, usar movimiento global
            move = new Vector3(horizontal, 0f, vertical);
        }

        // Si hay movimiento, rotar al personaje hacia esa dirección
        if (move.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            move = move.normalized * speed;
        }
        else
        {
            move = Vector3.zero;
        }

        // Agregar la componente vertical (salto y gravedad)
        move += new Vector3(0f, verticalVelocity, 0f);

        // Mover al personaje
        controller.Move(move * Time.deltaTime);
    }
}
