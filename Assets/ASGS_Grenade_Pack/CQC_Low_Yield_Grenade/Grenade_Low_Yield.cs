using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace ASGS.Grenade
{
    public class Grenade_Low_Yield : MonoBehaviour
    {
        public float delay = 3f;
        public float BlastRadiusLY = 5f;
        public float force = 500f;
        public float damage = 100;

        public GameObject explosionEffectLowYield;

        float countdown;
        bool hasExploded = false;

        // Start is called before the first frame update
        void Start()
        {
            countdown = delay;
        }

        // Update is called once per frame
        void Update()
        {
            countdown -= Time.deltaTime;
            if (countdown <= 0f && hasExploded == false)
            {
                Explode();
                hasExploded = true;
            }
        }

        void Explode()
        {
            //Show Explosion
            Instantiate(explosionEffectLowYield, transform.position, transform.rotation);

            //Get nearby objects
            Collider[] colliders = Physics.OverlapSphere(transform.position, BlastRadiusLY);

            foreach (Collider nearbyObject in colliders)
            {
                //Find distance from center of explosion
                float distFromCenter = Vector3.Distance(transform.position, nearbyObject.transform.position);
                float damageReduction = Mathf.Clamp01(1 - distFromCenter / BlastRadiusLY);
                if (damageReduction > 0)
                {
                    //Debug.Log(nearbyObject.name);
                    //Debug.Log(damageReduction);
                    //objectHealth = damage - damage reduction;
                }

                //Add Force
                Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    //find blast reduction based on distance from center
                    float blastReduction = force / Mathf.Clamp01(1 - distFromCenter / BlastRadiusLY);
                    //find force applied to object by reducing base force by blastReduction
                    float appliedForce = force - blastReduction;
                    //apply force to objects within range
                    rb.AddExplosionForce(appliedForce, transform.position, BlastRadiusLY);
                }

            }
            //Destroy Grenade Asset
            Destroy(gameObject);
        }
    }
}
