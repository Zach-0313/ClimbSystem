using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    // Start is called before the first frame update
    public float MaxStamina = 100;
    [SerializeField] float RechargeDelay;
    [SerializeField] float RechargeRate;
    [SerializeField] float DrainRate;
    public float Stamina;
    private Coroutine Recharger;
    public Text readout;
    void Start()
    {
        Stamina = MaxStamina;
        Recharger = StartCoroutine(RechargeTracker());
    }
    void Update()
    {
        readout.text = "Stamina = " + (100*(Stamina / MaxStamina)).ToString().Substring(0, (100 * (Stamina / MaxStamina)).ToString().IndexOf(".")) + "%";
    }

    public void DrainStamina(float multiplier)
    {
        if (Stamina == 0) return;
        Stamina = Mathf.MoveTowards(Stamina, 0, multiplier * DrainRate * Time.deltaTime);
        if (Recharger != null)
            StopCoroutine(Recharger);
        Recharger = StartCoroutine(RechargeTracker());
    }
    public void ChargeStamina(float multiplier)
    {
        if (Stamina == MaxStamina) return;
        Stamina = Mathf.MoveTowards(Stamina, MaxStamina, multiplier * RechargeRate * Time.deltaTime);
    }
    IEnumerator RechargeTracker()
    {

        yield return new WaitForSeconds(RechargeDelay);
        yield return null;

        while (Stamina <= MaxStamina)
        {
            ChargeStamina(1);
            yield return null;
        }
    }
}
