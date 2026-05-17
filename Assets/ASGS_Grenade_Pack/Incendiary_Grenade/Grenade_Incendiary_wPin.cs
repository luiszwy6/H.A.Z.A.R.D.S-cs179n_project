using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ASGS.Grenade
{
    public class Grenade_Incendiary_wPin : MonoBehaviour
    {
        public float delay = 2;
        public float BurnRange = 2f;
        public float force = 150f;
        public float damage = 30;
        public GameObject GrenadePrefab;
        public GameObject grenade;
        public GameObject ignition;
        public GameObject firePrefab;
        public GameObject fireObject;
        public GameObject pinPrefab;
        public Rigidbody pinRB;
        public Rigidbody HandleRB;
        public float IgniteThrust = 5;
        private float counter = 0;


        //Check if pin collider has left 
        void OnTriggerExit(Collider col)
        {
            if (col.gameObject.tag == "Grenade_Pin")
            {
                //basic check to prevent triggering multiple times
                counter++;

                if (counter == 1)
                {
                    //Debug.Log("Pin Out");
                    Invoke(nameof(releaseHandle), delay - 1);
                    Invoke(nameof(Ignite), 2);
                    Invoke(nameof(Explode), 4);
                    pinRB.isKinematic = false;
                }
                else return;
            }
        }

        void releaseHandle()
        {
            //Sets the Rigidbody of the Handle to false
            HandleRB.isKinematic = false;
            //Launches the handle with the predefined or modified amount
            //of thrust upward based on assets current angle
            HandleRB.AddForce(IgniteThrust * transform.up, ForceMode.Impulse);
            HandleRB.AddForce(transform.forward * IgniteThrust / 4, ForceMode.Impulse);
        }

        void Ignite()
        {
            ignition.SetActive(true);
        }
        void Explode()
        {
            //Spawn Incendiary Effect Prefab
            fireObject = Instantiate(firePrefab, transform.position, Quaternion.identity);

            //Get nearby objects
            Collider[] colliders = Physics.OverlapSphere(transform.position, BurnRange);

            foreach (Collider nearbyObject in colliders)
            {
                //Add Force
                Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    //Find damage reduction value based on distance from the grenade
                    float damageReduction = 1 / (transform.position - nearbyObject.transform.position).sqrMagnitude;

                    if (damageReduction > 0)
                    {
                        //Set up your objects to receive damage here
                        //objectHealth = damage * damageReduction;
                    }
                    //Reduce force applied based on the damage reduction
                    float appliedForce = force * damageReduction;
                    //apply force to objects within range
                    rb.AddExplosionForce(appliedForce, transform.position, BurnRange);
                }

            }
            Destroy(grenade);
            Invoke("End", 28);

        }
        void End()
        {
            Destroy(fireObject);
            Destroy(GrenadePrefab);
        }
    }
}

