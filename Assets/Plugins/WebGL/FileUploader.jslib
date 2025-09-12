mergeInto(LibraryManager.library, {
  UploadFile: function (gameObjectNamePtr, methodNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.png,.jpg,.jpeg';

    input.onchange = function (event) {
      var file = event.target.files[0];
      if (!file) return;

      var reader = new FileReader();
      reader.onload = function () {
        var data = reader.result.split(',')[1]; // Get base64 content
        SendMessage(gameObjectName, methodName, data);
      };
      reader.readAsDataURL(file);
    };

    input.click();
  }
});
