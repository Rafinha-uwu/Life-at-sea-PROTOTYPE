using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GAME : MonoBehaviour
{
    public GameObject Fade;
    public GameObject Player;

    public GameObject HUB;
    public GameObject TargetHUB;
    public GameObject Level1;
    public GameObject Target1;

    public void Awake ()
    {
        Player.transform.position = TargetHUB.transform.position;
    }

    public void L1()
    {
        StartCoroutine(StartL1());
    }

    public void Hub()
    {
        StartCoroutine(GoHub());
    }

    public IEnumerator StartL1()
    {
        Fade.GetComponent<Animator>().Play("In");

        yield return new WaitForSeconds(4f);

        Level1.SetActive(true);

        yield return new WaitForSeconds(.5f);

        Player.transform.position = Target1.transform.position;
        HUB.SetActive(false);

        yield return new WaitForSeconds(.5f);

        Fade.GetComponent<Animator>().Play("Out");

    }

    public IEnumerator GoHub()
    {
        Fade.GetComponent<Animator>().Play("In");

        yield return new WaitForSeconds(4f);

        HUB.SetActive(true);

        yield return new WaitForSeconds(.5f);

        Player.transform.position = TargetHUB.transform.position;
        Level1.SetActive(false);

        yield return new WaitForSeconds(.5f);

        Fade.GetComponent<Animator>().Play("Out");

    }
}
