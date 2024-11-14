using UnityEngine;

namespace UniCAVE
{
    public class Move : MonoBehaviour
    {
        void Update()
        {
            //transform.localPosition = new Vector3(Mathf.Sin(Time.time), Mathf.Cos(Time.time), -1);
            if (Input.GetKey("up"))
            {
                print("up arrow key is held down");
            }

            if (Input.GetKey("down"))
            {
                print("down arrow key is held down");
            }
            //public float RotationSpeed;
            //private Quaternion lookRotation;
            //private Vector3 direction;
            float moveSpeed = 2;
            //Define the speed at which the object moves.

            float horizontalInput = Input.GetAxis("Horizontal");
            //Get the value of the Horizontal input axis.

            float verticalInput = Input.GetAxis("Vertical");
            //Get the value of the Vertical input axis.

            transform.Translate(new Vector3(horizontalInput, 0, verticalInput) * moveSpeed * Time.deltaTime);
            //Move the object to XYZ coordinates defined as horizontalInput, 0, and verticalInput respectively.
            // mouse = Input.mousePosition;
            //castPoint = Camera.main.ScreenPointToRay(mouse);
            //I'm not sure if castPoint has a position in it or not, but if when you type castPoint.transform.position
            //direction = (castPoint.- transform.position).normalized;

            //create the rotation we need to be in to look at the target
            //lookRotation = Quaternion.LookRotation(direction);

            //rotate us over time according to speed until we are in the required rotation
            //transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime* RotationSpeed);
            float rotSpeed = 1;
            float rotX = Input.GetAxis("Mouse X") * rotSpeed * Mathf.Deg2Rad;
            //float rotY = Input.GetAxis("Mouse Y") * rotSpeed * Mathf.Deg2Rad;

            //transform.RotateAround(Vector3.up, -rotX);
            transform.RotateAround(Vector3.up, rotX);
            //transform.RotateAround(Vector3.right, rotY);
        }


        //void OnMouseDrag()
        //{
        //float rotSpeed = 20;
        //float rotX = Input.GetAxis("Mouse X") * rotSpeed * Mathf.Deg2Rad;
        //float rotY = Input.GetAxis("Mouse Y") * rotSpeed * Mathf.Deg2Rad;

        //transform.RotateAround(Vector3.up, -rotX);
        //transform.RotateAround(Vector3.right, rotY);
        //}
    }
}