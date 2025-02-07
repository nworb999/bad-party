﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardMoveCharacter : MonoBehaviour
{
    public float speed = 6.0f;
    public float sprintMultiplier = 1.5f; // Multiplier for sprint speed
    public float gravity = -10f;
    public float jumpSpeed = 15.0f;
    public float minFall = -1.5f;
    public float pushForce = 3.0f;

    private float _vertSpeed;
    private CharacterController _charController;

    // Start is called before the first frame update
    void Start()
    {
        _charController = GetComponent<CharacterController>();
        _vertSpeed = minFall;
    }

    // Update is called once per frame
    void Update()
    {
        float deltaX = Input.GetAxis("Horizontal") * speed;
        float deltaZ = Input.GetAxis("Vertical") * speed;
        Vector3 movement = new Vector3(deltaX, 0, deltaZ);
        movement = Vector3.ClampMagnitude(movement, speed);

        // Apply sprint multiplier if shift is held
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            movement *= sprintMultiplier;
        }

        //check if character is on the ground
        if (_charController.isGrounded)
        {
            //check if we've pressed 'jump'
            if (Input.GetButtonDown("Jump"))
            {
                //we're on the ground and pressed jump
                //so add jumpSpeed (a positive vertical speed)
                //to our vertical speed
                _vertSpeed += jumpSpeed;
            }
            else
            {
                //we're on the ground but didn't press jump
                //keep a minimum vertical speed (negative)
                //so we stay on the ground
                _vertSpeed = minFall;
            }
        }
        else
        {
            //character is not on the ground. don't check
            //if we pressed jump. add gravity (negative to our
            //vertical speed
            _vertSpeed += gravity * Time.deltaTime;
        }

        movement.y = _vertSpeed;

        movement *= Time.deltaTime;
        movement = transform.TransformDirection(movement);

        _charController.Move(movement);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body != null && !body.isKinematic)
        {
            Debug.Log("pushing");
            body.velocity = hit.moveDirection * pushForce;
        }
    }
}
