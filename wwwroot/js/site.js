/*
  site.js - JavaScript dùng chung cho toàn website. Gồm 4 việc nhỏ:
   1) Hiện thông báo nhanh (toast) ở góc màn hình sau khi thêm giỏ, lưu dữ liệu...
   2) Cửa sổ "Chi tiết" sản phẩm ở trang chủ (đọc dữ liệu từ các thuộc tính data-*)
   3) Xem trước các ảnh vừa chọn trong form sản phẩm (trang quản trị)
   4) Hỏi xác nhận trước khi gửi form có thuộc tính data-confirm (ví dụ nút Xóa)
*/
(function () {
  'use strict';
  var $ = function (id) { return document.getElementById(id); };

  // Toast: hiện thông báo (do server đặt vào TempData["Toast"])
  var toast = $('toast');
  if (toast && toast.textContent.trim() !== '') {
    toast.classList.add('show');
    setTimeout(function () { toast.classList.remove('show'); }, 2600);
  }

  // Modal chi tiết sản phẩm (trang cửa hàng)
  var backdrop = $('modalBackdrop');
  if (backdrop) {
    var closeModal = function () { backdrop.classList.remove('show'); };
    document.querySelectorAll('[data-detail]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var d = btn.dataset;
        $('modalTitle').textContent = d.name;
        backdrop.querySelectorAll('[data-f]').forEach(function (el) {
          el.textContent = d[el.dataset.f] || '';
        });
        backdrop.querySelector('input[name="id"]').value = d.id;
        var img = $('modalImage');
        if (img) {
          if (d.image) { img.src = d.image; img.hidden = false; }
          else { img.hidden = true; img.removeAttribute('src'); }
        }
        var addBtn = backdrop.querySelector('button.admin-btn');
        if (addBtn) addBtn.disabled = Number(d.stock) < 1;
        backdrop.classList.add('show');
      });
    });
    var closeBtn = $('closeModal');
    if (closeBtn) closeBtn.addEventListener('click', closeModal);
    backdrop.addEventListener('click', function (e) { if (e.target === backdrop) closeModal(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeModal(); });
  }

  // Xem trước các ảnh vừa chọn ở form sản phẩm (quản trị)
  var fileInput = $('ImageFiles');
  var previews = $('newImagePreviews');
  if (fileInput && previews) {
    fileInput.addEventListener('change', function () {
      previews.innerHTML = '';
      Array.prototype.forEach.call(fileInput.files, function (f) {
        var img = document.createElement('img');
        img.src = URL.createObjectURL(f);
        img.alt = f.name;
        previews.appendChild(img);
      });
    });
  }

  // Xác nhận trước khi submit form có data-confirm (ví dụ: xóa sản phẩm)
  document.addEventListener('submit', function (e) {
    var msg = e.target && e.target.dataset ? e.target.dataset.confirm : null;
    if (msg && !window.confirm(msg)) e.preventDefault();
  });
})();
