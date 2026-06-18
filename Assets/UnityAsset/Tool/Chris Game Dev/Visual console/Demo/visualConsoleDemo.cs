using UnityEngine;
using chrisGameDev.VisualConsole;


namespace chrisGameDev.VisualConsole
{ 
    internal class visualConsoleDemo : MonoBehaviour
    {
        private GameObject test;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
            Debug.Log("Defaul Unity Log - Demo Start().");
                VisualDebug.Log(
                "VISUAL CONSOLE Log - Demo Start().",
                "blueviolet",
                16,
                FontStyle.Bold,
                "powderblue",
                6
                );

            //StartCoroutine(method_demo());
            method_demo_A();
            method_demo_B();

            //Debug.Log("This is an error that interrupts visual logs display. Just un-pause to continue showing logs: " + test.transform.tag);
            Debug.Log("Scope can be difficult to understand...");
        }

        //private IEnumerator method_demo()
        private void method_demo_A()
        {
            
            VisualDebug.NestingLog("nest1",
                "☺ method_demo_A() ► This is a Nesting Log (Double click to FOLD)",
                "orange",
                13,
                FontStyle.Bold,
                "brown",
                12);

            //yield return new WaitForSeconds(2);
            Debug.Log("☺ Right after the nesting starts.");
            //yield return new WaitForSeconds(2);
            VisualDebug.Log(
                "Many logs can be nested.",
                "greenyellow",
                10,
                FontStyle.Normal,
                "lightslategray"
                );
            //yield return new WaitForSeconds(2);
            VisualDebug.Log(
                "But the nesting must stop somewhere.",
                "khaki",
                13,
                FontStyle.Normal,
                "forestgreen"
                );
            //yield return new WaitForSeconds(1);
            VisualDebug.NestingLogEnd("nest1");
            //yield return new WaitForSeconds(1);
            Debug.Log("Right after the nesting ends.");
      
        }

        private void method_demo_B()
        {
            VisualDebug.NestingLog("nest2",
                "♥ method_demo_B() ► This is a BIG nesting log...",
                "white",
                15,
                FontStyle.Bold,
                "black",
                12);

            Debug.Log("Which can contain...");
            VisualDebug.Log("Logs with stacktracing (click us!)", "aquamarine", 14, FontStyle.Italic, "darkviolet", 12);
            method_demo_C();
            Debug.Log("Nesting or scope are useful concepts.");
            Debug.Log("Visualizing can make things easier to understand.");
            
            VisualDebug.NestingLogEnd("nest2");
        }

        private void method_demo_C()
        {
            VisualDebug.NestingLog("nest3",
                "♦ method_demo_C() ► Nested inside another log...",
                "lawngreen",
                13,
                FontStyle.BoldAndItalic,
                "dodgerblue",
                10);

            Debug.Log("Nesting and normal types of logs...");
            method_demo_D();
            Debug.Log("This can be very useful...");

            VisualDebug.NestingLogEnd("nest3");
        }

        private async void method_demo_D()
        {
            VisualDebug.NestingLog("nest4Async",
                "♣ method_demo_D() ► Awaiting a few seconds (Awaitable.WaitForSecondsAsync())...",
                "cornsilk",
                11,
                FontStyle.BoldAndItalic,
                "chocolate",
                1);

            await Awaitable.WaitForSecondsAsync(2f);


            Debug.Log("Log that awaited a few seconds.");
            VisualDebug.NestingLogEnd("nest4Async");

            method_demo_E();
        }

        private void method_demo_E()
        {
            VisualDebug.Log(
                "Demo ends here ☻.",
                "maroon",
                18,
                FontStyle.BoldAndItalic,
                "orange",
                20
                );
        }

    }
}