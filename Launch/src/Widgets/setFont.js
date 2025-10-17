(function () {
  const fontName = "Digital";
  const fontUrl = "../fonts/TickingTimebombBB.ttf";

  const style = document.createElement("style");
  style.textContent = `
    @font-face {
      font-family: '${fontName}';
      src: url('${fontUrl}') format('truetype');
    }

    * {
      font-family: '${fontName}', sans-serif !important;
    }
  `;
  document.head.appendChild(style);
})();

