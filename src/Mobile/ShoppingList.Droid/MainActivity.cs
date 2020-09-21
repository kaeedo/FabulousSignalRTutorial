using System.Net;
using Android.App;
using Android.OS;
using Android.Runtime;

using Xamarin.Forms.Platform.Android;

namespace ShoppingList.Droid
{
    [Activity(Label = "@string/app_name", Theme = "@style/MainTheme", MainLauncher = true)]
    public class MainActivity : FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Xamarin.Forms.Forms.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource

            this.LoadApplication(new ShoppingList.App());
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}