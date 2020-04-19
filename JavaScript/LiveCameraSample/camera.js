// this sample API URL corresponds to Computer Vision API v2.0 - Describe Image
// please refer to the Computer Vision API Reference for API usage
var API_URL = 'https://westus.api.cognitive.microsoft.com/vision/v2.0/describe'

// replace this with your own API KEY
var API_KEY = '---YOUR_API_KEY---'

// this is the rate of how frequent your camera image is sent to the Computer Vision API
var CAPTURE_RATE = 3000  // milliseconds

// camera settings
var CONSTRAINTS = {
  audio: false
  , video: { facingMode: 'environment' }     // using the rear camera
  //, video: { facingMode: 'user' }          // using the front camera
  , video: { exact: {
    height: 500      // change this to your desired image height
    , width: 800 }      // change this to your desired image width
  }
}

async function cameraStart() {
  try {
    const stream = await navigator.mediaDevices.getUserMedia(CONSTRAINTS)

    video.srcObject = stream
    video.play()

    var canvas = document.getElementById('canvas')
    canvas.style.display = 'none'
    var context = canvas.getContext('2d')

    setInterval(
      function () {
        context.drawImage(video, 0, 0, 680, 480)
        sendImageToAPI()   // invoke the call to Computer Vision API
      }
      , CAPTURE_RATE
    )

  } catch (err) {
    throw err
  }
}

// send your camera image to Computer Vision API
function sendImageToAPI() {

  var payload = dataURItoBlob()
  console.log(payload)
  
  $.ajax({
    type: 'POST'
    , url: API_URL
    , headers: {
      'Content-Type': 'application/octet-stream'
      , 'Ocp-Apim-Subscription-Key': API_KEY
    }
    , data: payload
    , processData: false
    , success: function (data) {
        console.log(data)

        var captionsResult = ''
        $.each(data.description.captions, function(index, value) {
          captionsResult = captionsResult + '<p>' + value.text + ' [confidence: ' + value.confidence + '] </p>'
        })
        changeInnerHtml('.captions', captionsResult)

        var tagsResult = ''
        $.each(data.description.tags, function(index, value) {
          tagsResult = tagsResult + value + '  '
        })
        changeInnerHtml('.tags', tagsResult)
    }
    , error: function (xhr, status) {
        alert('API POST Error.')
    }
  })
}

// helper functions - DO NOT CHANGE IF YOU DONT UNDERSTAND THE CODE BELOW
function dataURItoBlob() {
  var dataUri = canvas.toDataURL('image/jpeg')
  var data = dataUri.split(',')[1]
  var mimeType = dataUri.split('')[0].slice(5)

  var bytes = window.atob(data)
  var buf = new ArrayBuffer(bytes.length)
  var byteArr = new Uint8Array(buf)

  for (var i = 0; i < bytes.length; i++) {
      byteArr[i] = bytes.charCodeAt(i)
  }
  return byteArr
}

function changeInnerHtml(elementPath, newText){
  $(elementPath).fadeOut(500, function() {
      $(this).html(newText).fadeIn(500)
  })
}