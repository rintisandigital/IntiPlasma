(function ($) {
  "use strict";

  var rangeSliderOne = function () {
    if ($("#basic-slider-one").length > 0) {
      var skipSlider = document.getElementById("basic-slider-one");
      noUiSlider.create(skipSlider, {
        start: 25,
        connect: [true, false],
        behaviour: "snap",
        step: 1,
        range: {
          min: 0,
          max: 100,
        },
        format: {
          from: function (value) {
            return parseInt(value);
          },
          to: function (value) {
            return parseInt(value);
          },
        },
      });

      var dirVal = document.querySelector(".field-dir");
      skipSlider.noUiSlider.on("update", function (val, e) {
        dirVal.innerHTML = val[e];
      });
    }
  };

  var rangeSliderTwo = function () {
    if ($("#price-value-range").length > 0) {
      var skipSlider = document.getElementById("price-value-range");
      var skipValues = [
        document.getElementById("price-min-value"),
        document.getElementById("price-max-value"),
      ];

      noUiSlider.create(skipSlider, {
        start: [18, 32],
        connect: true,
        step: 1,
        range: {
          min: 0,
          max: 100,
        },
        format: {
          from: function (value) {
            return parseInt(value);
          },
          to: function (value) {
            return parseInt(value);
          },
        },
      });

      skipSlider.noUiSlider.on("update", function (val, e) {
        skipValues[e].innerText = val[e];
      });
    }
  };

  var rangeSliderTooltip = function () {
    if ($("#slider-tooltip").length > 0) {
      var skipSlider = document.getElementById("slider-tooltip");

      noUiSlider.create(skipSlider, {
        start: [30, 70],
        connect: true,
        tooltips: [true, true],
        step: 1,
        range: {
          min: 0,
          max: 100,
        },
        format: {
          from: function (value) {
            return parseInt(value);
          },
          to: function (value) {
            return parseInt(value);
          },
        },
      });
    }
  };

  var rangeSliderDisable = function () {
    if ($("#slider-disable").length > 0) {
      var skipSlider = document.getElementById("slider-disable");
      var checkbox = document.getElementById("checkbox-slider");
      function handleCheck(e) {
        if (this.checked) {
          e.setAttribute("disabled", true);
        } else {
          e.removeAttribute("disabled");
        }
      }

      noUiSlider.create(skipSlider, {
        start: 50,
        connect: [true, false],
        range: {
          min: 0,
          max: 100,
        },
        format: {
          from: function (value) {
            return parseInt(value);
          },
          to: function (value) {
            return parseInt(value);
          },
        },
      });

      checkbox.addEventListener("click", function () {
        handleCheck.call(this, skipSlider);
      });
    }
  };

  var rangeTwoKeypress = function () {
    if ($("#keypress-two-val").length > 0) {
      var skipSlider = document.getElementById("keypress-two-val");
      var skipValues = [
        document.getElementById("ip-val-lower"),
        document.getElementById("ip-val-upper"),
      ];

      noUiSlider.create(skipSlider, {
        start: [30, 70],
        connect: true,
        tooltips: [true, true],
        step: 1,
        range: {
          min: 0,
          max: 100,
        },
        format: {
          from: function (value) {
            return parseInt(value);
          },
          to: function (value) {
            return parseInt(value);
          },
        },
      });

      skipSlider.noUiSlider.on("update", function (val, e) {
        skipValues[e].value = val[e];
      });
    }
  };

  var rangeSoftLimit = function () {
    if ($("#limit-val").length > 0) {
      var limitVal = document.getElementById("limit-val");
      noUiSlider.create(limitVal, {
        start: 50,
        range: {
          min: 0,
          max: 100,
        },
        pips: {
          mode: "values",
          values: [20, 80],
          density: 5,
        },
      });
      var limitHandle = limitVal.noUiSlider;
      limitHandle.on("change", function (val, e) {
        if (val[e] < 20) {
          this.set(20);
        } else if (val[e] > 80) {
          this.set(80);
        }
      });
    }
  };
  var rangePips = function () {
    if (jQuery("#pips-val").length > 0) {
      var pipsSlider = document.getElementById("pips-val");
      noUiSlider.create(pipsSlider, {
        range: {
          min: 0,
          max: 100,
        },
        start: [50],
        pips: {
          mode: "count",
          values: 5,
          density: 5,
        },
      });
    }
  };

  var rangeDirection = function () {
    if ($("#slider-dir").length > 0) {
      var dirSlider = document.getElementById("slider-dir");
      noUiSlider.create(dirSlider, {
        start: 80,
        range: {
          min: 0,
          max: 100,
        },
      });

      var dirVal = document.querySelector(".field-dir");
      dirSlider.noUiSlider.on("update", function (val, e) {
        dirVal.innerHTML = val[e];
      });
    }
  };

  
  var rangeSalary = function () {
    if ($("#salary-val").length > 0) {
      var skipSlider = document.getElementById("salary-val");
      var skipValues = [
        document.getElementById("salary-val-lower"),
        document.getElementById("salary-val-upper"),
      ];
  
      const chart = document.getElementById("salary-chart");
      const totalBars = 30;
      const minValue = 0;
      const maxValue = 206;
      const stepValue = (maxValue - minValue) / totalBars;
  
      for (let i = 0; i < totalBars; i++) {
        const bar = document.createElement("span");
    
        const minHeight = 40; 
        const maxHeight = 100;
        const height = Math.floor(Math.random() * (maxHeight - minHeight + 1)) + minHeight;
      
        bar.style.height = `${height}%`;
        chart.appendChild(bar);
      }
  
      const bars = chart.querySelectorAll("span");
  
      noUiSlider.create(skipSlider, {
        start: [0, 200],
        connect: true,
        tooltips: [true, true],
        step: 1,
        range: {
          min: 0,
          max: 200,
        },
        format: {
          from: function (value) {
            return Number(value.replace('$', '', 'K'));
          },
          to: function (value) {
            return '$' + parseInt(value) + 'K';
          }
        },
      });
  
      skipSlider.noUiSlider.on("update", function (values) {
        const lower = parseInt(values[0].replace(/\$|K/g, ""));
        const upper = parseInt(values[1].replace(/\$|K/g, ""));
  
        bars.forEach((bar, i) => {
          const value = i * stepValue;
          if (value >= lower && value <= upper) {
            bar.classList.add("active");
          } else {
            bar.classList.remove("active");
          }
        });
      });
    }
  };

  $(function () {
    rangeSliderOne();
    rangeSliderTwo();
    rangeSliderTooltip();
    rangeSliderDisable();
    rangeTwoKeypress();
    rangeSoftLimit();
    rangePips();
    rangeDirection();
    rangeSalary();
  });
})(jQuery);
