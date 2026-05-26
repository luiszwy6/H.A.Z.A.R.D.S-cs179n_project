Thank you for purchasing the A Square Games and Simulation Grenade pack!

Below is some helpful info of the different grenade scripts functions. 

-Inside of the "Grenade_Smoke_wPin" script is a basic setup for adding an additional trigger. Currently the grenades only check if the pin has been pulled. The intention of this is to allow grenades to be tossed with the pin still in (maybe tossing to an ally). You can use the commented section and add a trigger that checks "PinPulled" & "Thrown" thrown should be something triggered via a character/npc controller.

-You could remove this and just make an animation be the trigger instead of checking if the pin has left the collider by changing the trigger event and just using the code inside.

Main grenade functions breakdown: Each area of the code should contain comments as well to better understand each section.

- "OnTriggerExit()"
	There is a collider setup on the grenade around the pin. You can modify this in the prefab to make the collider more precise as desired. There is a tag setup on the pin asset simply named "Pin" onTriggerExit simply checks to see if the pin leaves the collider. It then invokes the other functions contained within and makes the pin no longer kinematic. A counter was added which is iterated here to prevent the grenades invoked statements from being triggered multiple times.

- "releaseHandle()"
	This section simply adds gravity to the handle and then launches it with some force to mimic the handle popping off of a real grenade.

- "Ignite()"
	This function activates the Smoke ignition (or in the incendiary grenade's case the sparking and initial smoke) and then deactivates the smoke cover at the bottom of the grenade (you can grab the code within releaseHandle() to instead launch this off instead if desired)

- "Smokescreen()"
	This function instantiates the defined smokescreen prefab (on the attached assets script) at the grenades location, then it simply starts the "End()" function after a predefined time, plus the delay time desired, and destroys the smoke grenade mesh (or whatever you define) after 15 seconds have past that way the grenade disappears inside of the smokescreen.

- "Explode()"
	This function instantiates an explosion effect and applies force on the surrounding objects and provides some damage based on range from the center point.

- "End()"
	This function simply destroys the gameObject and the instantiated Smokescreen so they don't linger in the scene.