const weather = document.createElement('div');
const body = document.body
const img = document.createElement("img");

weather.prepend(img);

const temp = document.createElement('p');

weather.appendChild(temp)
body.appendChild(weather);

weather.style.position = 'absolute';
weather.className = 'widget';
weather.id = 'weather';

async function loadWidget() {
    const ipRes = await fetch("https://ipwho.is/");
    const ipData = await ipRes.json();
    let lon = ipData.longitude;
    let lat = ipData.latitude;
    console.log(" lon: " + lon + " lat: " + lat);
    const response2 = await fetch(`http://api.weatherapi.com/v1/current.json?key=1b7219347ef742b9b11181215232409&q=${lon},${lat}&aqi=no`);
    const W_data = await response2.json();

    img.src = `https:${W_data["current"]["condition"]["icon"]}`;
    temp.textContent = `${Math.round(W_data['current']['temp_c'])}  C`;
}

(async function init() {
    loadWidget();
    setInterval(loadWidget, 1800000);
})();

function openInBrowser() {
    const url = 'https://weather.com/';
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ type: 'openBrowser', url });
  } else {
    window.open(url, '_blank');
  }
}
