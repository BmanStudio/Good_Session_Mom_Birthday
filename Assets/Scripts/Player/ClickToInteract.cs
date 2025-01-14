﻿using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ClickToInteract : MonoBehaviour
{
    [Header("SphereCast settings")]
    [SerializeField] float tapSphereCastRadius = .2f;
    [SerializeField] float checkForObjectSphereCastRadius = .2f;

    [Header("Player Interaction Settings")]
    [SerializeField] private float rotationToInteractSpeed = 5f;

    [Header("Camera Settings")]
    [SerializeField] float minZoomFOV = 20f;
    [SerializeField] float maxZoomFOV = 105f;
    [SerializeField] float scrollWheelZoomFactor = 100;

    [SerializeField] private float zoomRequested = 50;
    [SerializeField] float zoomSpeed = 10;

    [SerializeField] float camMoveSpeed = 10;
    [SerializeField] Vector3 cameraOffsetFromPlayer;
    [SerializeField] float cameraOffsetFromPlayerFixZFactor = 2f;

    private IInteractable currentInteractingWith = null;
    private Vector3 currentInteractDest;

    private Vector3 newCamPos;
    private Vector3 camStartPos;

    private NavMeshAgent mNavMeshAgent;

    private bool onWayToInteractDest = false;
    private bool isRotatingToInteract = false;

    private float tapWaitTime = .1f; // Its about time to fix the zoom glitch
    private bool tapTimerOn = false; // Its about time to fix the zoom glitch

    private void Awake()
    {
        mNavMeshAgent = GetComponent<NavMeshAgent>();
        camStartPos = Camera.main.transform.position;
        newCamPos = camStartPos;
    }

    void Update()
    {
        if (!GameManager.instance.isGameInPlayState) { return; }

        if (Input.touchCount == 2)
        {
            tapTimerOn = false;
            PinchZoom();
        }  // checking for pinch to zoom first

        else if (Input.touchCount > 2)
        {
            tapTimerOn = false;
            return;
        } // TODO some visual explaination, no more than 2 fingers commands

        else if (Input.GetMouseButtonDown(0))
        {
            StartCoroutine("TapZoomGlitchFixer");
        }  // the main tap handler

        if (isRotatingToInteract)
        {
            var targetRotation = Quaternion.LookRotation(currentInteractDest - transform.position);

            RaycastHit hit;
            if (Physics.SphereCast(transform.position, checkForObjectSphereCastRadius, transform.forward, out hit))
            {
                var temp = hit.transform.GetComponent<IInteractable>();
                if (temp == currentInteractingWith)
                {
                    OnReachedDest();
                    isRotatingToInteract = false;
                }
            }

            // Smoothly rotate towards the target point.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationToInteractSpeed * Time.deltaTime);
        } // rotates the player to pick up item that within the stoppingdistance range

        else if (onWayToInteractDest)
        {
            UpdateDest();

            // Check if we've reached the destination
            if (!mNavMeshAgent.pathPending)
            {
                if (mNavMeshAgent.remainingDistance <= mNavMeshAgent.stoppingDistance)
                {
                    if (!mNavMeshAgent.hasPath || mNavMeshAgent.velocity.sqrMagnitude == 0f)
                    {
                        OnReachedDest();
                    }
                }
            }
        } // The checking if we should interact with the destination. Interaction get called here

        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            Zoom(Input.GetAxis("Mouse ScrollWheel") * scrollWheelZoomFactor);
        }  // for PC / debugging

        CameraPosUpdater(); // Updating the position of the camera according to the player

    }

    private IEnumerator TapZoomGlitchFixer()
    {
        if (!PlayerManager.instance.isPlayerAbleToControl) { yield break; }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, tapSphereCastRadius);
        if (hits.Length == 0)
        {
            yield break;
        }
        else 
        {
            tapTimerOn = true;
            yield return new WaitForSeconds(tapWaitTime);
        }

        if (tapTimerOn)
        {
            InteractWithTap(hits);
        }
    }

    private void InteractWithTap(RaycastHit[] hits)
    {
        for (int i = 0; i <= hits.Length - 1; i++)
        {
            //var interactable = hits[i].transform.GetComponent<IInteractable>();
            var interactable = hits[i].transform.GetComponentInChildren<IInteractable>();
            if (interactable != null)
            {
                if (interactable.GetInteractType() == InteractType.PickableToy)
                {
                    if (PlayerManager.instance.isHoldingClientHand)
                    {
                        GameManager.instance.PlayClickFailSound();
                        continue;
                    }
                    else
                    {
                        InteractWithPickableToy(interactable);
                        return;
                    }
                }

                else if (interactable.GetInteractType() == InteractType.StaticToy)
                {
                    InteractWithStaticToy(interactable);
                    return;
                }

                else if (interactable.GetInteractType() == InteractType.Client)
                {
                    InteractWithClient(interactable);
                    return;
                }

                else if (interactable.GetInteractType() == InteractType.Move)
                {
                    InteractWithFloor(hits, i);
                }
            }
        }
    }

    private void InteractWithStaticToy(IInteractable interactable)
    {
        currentInteractingWith = interactable;
        currentInteractDest = currentInteractingWith.GetInteractionPoint();
        onWayToInteractDest = true;
    }

    private void InteractWithFloor(RaycastHit[] hits, int i)
    {
        if (onWayToInteractDest) { onWayToInteractDest = false; } // if the player currenty going somewhere but changing his mind and want to walk away
        if (isRotatingToInteract) { isRotatingToInteract = false; } // if the player currenty rotation to object but changing his mind and want to walk away
        mNavMeshAgent.SetDestination(hits[i].point);
    }

    private void InteractWithClient(IInteractable interactable)
    {
        if (!interactable.GetIsCurrentlyInteractable())
        {
            GameManager.instance.PlayClickFailSound();
            return;
        }

        if (PlayerManager.instance.isHoldingClientHand)
        {
            if (PlayerManager.instance.currentlyHoldingClient == interactable)
            {
                interactable.OnInteraction(); // to stop holding this child's hand
            }

            else
            {
                GameManager.instance.PlayClickFailSound();
            }
        }

        else
        {
            // Checking if the object is not our current object and it within the stopping distance of the navmesh agent, so we could turn around
            if (interactable != currentInteractingWith &&
                Vector3.Distance(transform.position, interactable.GetInteractionPoint())
                < mNavMeshAgent.stoppingDistance)
            {
                isRotatingToInteract = true;
            }

            currentInteractingWith = interactable;
            currentInteractDest = currentInteractingWith.GetInteractionPoint();
            onWayToInteractDest = true;
        }
    }

    private void InteractWithPickableToy(IInteractable interactable)
    {
        if (!interactable.GetIsCurrentlyInteractable())
        {
            GameManager.instance.PlayClickFailSound();
            return;
        }

        // Checking if the object is not our current object and it within the stopping distance of the navmesh agent, so we could turn around
        if (interactable != currentInteractingWith &&
            Vector3.Distance(transform.position, interactable.GetInteractionPoint())
            < mNavMeshAgent.stoppingDistance)
        {
            if (interactable != PlayerManager.instance.GetIInteractableHeld()) // to fix a bug where you look at a object you hold
            {
                isRotatingToInteract = true;
            }
        }

        currentInteractingWith = interactable;
        currentInteractDest = currentInteractingWith.GetInteractionPoint();
        onWayToInteractDest = true;
    }

    private void PinchZoom()
    {
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
        Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

        float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
        float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

        float difference = currentMagnitude - prevMagnitude;

        Zoom(difference * 0.05f);
    }

    private void CameraPosUpdater()
    {
        // Multiply the offset x and z by the camera position, so the offset wil be in the opposite direction
        float xMultiplier = 1;
        float zMultiplier = 1;

        // To fix the issue with the opposite sides view
        float fixedZ = cameraOffsetFromPlayer.z;


        if (transform.position.x > 0) // checks if the camera is in the left side of the map
        {
            xMultiplier = -1;
        }

        if (transform.position.z > 0) // checks if the camera is in the bottom side of the map
        {
            zMultiplier = -1;

            // here we adding to the Z factor
            fixedZ += cameraOffsetFromPlayerFixZFactor;
        }

        fixedZ *= zMultiplier;

        var fixedOffset = new Vector3(cameraOffsetFromPlayer.x * xMultiplier, 0, fixedZ);

        // Calculates the relative position to the player by the fraction of the zoom
        // the more you zoom - the closer you get.
        float fraction = (1 - (zoomRequested / maxZoomFOV));

        var temp = (transform.position - camStartPos) * fraction - (fixedOffset * fraction);

        newCamPos = new Vector3(camStartPos.x + temp.x, camStartPos.y, camStartPos.z + temp.z);

        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, newCamPos, camMoveSpeed * Time.deltaTime);

    }

    private void LateUpdate()
    {

        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, zoomRequested, zoomSpeed * Time.deltaTime);

    }

    private void Zoom(float increment)
    {
        float amount = Camera.main.fieldOfView - increment;
        zoomRequested = Mathf.Clamp(amount, minZoomFOV, maxZoomFOV);
    }

    private void OnReachedDest()
    {
        onWayToInteractDest = false;
        currentInteractingWith.OnInteraction();
    }

    private void UpdateDest()
    {
        currentInteractDest = currentInteractingWith.GetInteractionPoint();
        mNavMeshAgent.SetDestination(currentInteractDest);
    }

}
