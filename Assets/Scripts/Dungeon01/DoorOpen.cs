using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DoorOpen : MonoBehaviour {

	public float TheDistance;
	public GameObject ActionDisplay;
	public GameObject ActionText;
    public GameObject theLever;
    public GameObject blueGate;
    public AudioSource leverSound;
    public AudioSource gateSound;

	void Update () {
		TheDistance = PlayerCasting.DistanceFromTarget;
	}

	void OnMouseOver () {
		if (TheDistance <= 3) {
            ActionText.GetComponent<Text>().text = "Open";
			ActionDisplay.SetActive (true);
			ActionText.SetActive (true);
		}

		if (Input.GetButtonDown ("Action")) {
			if (TheDistance <= 3) {
				ActionDisplay.SetActive (false);
				ActionText.SetActive (false);
                this.gameObject.GetComponent<BoxCollider>().enabled = false;
              //  theLever.GetComponent<Animation>().Play("SwitchDownAnim");
				theLever.GetComponent<Animator>().enabled = true;
				leverSound.Play();
                StartCoroutine(GateOpen());
			}
		}
	}

	void OnMouseExit() {
		ActionDisplay.SetActive (false);
		ActionText.SetActive (false);
	}

    IEnumerator GateOpen()
    {
        yield return new WaitForSeconds(1);
        blueGate.GetComponent<Animator>().Play("BlueGateOpen");
        gateSound.Play();
    }

}

