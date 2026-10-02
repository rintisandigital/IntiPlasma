/*
Author       : Dreamstechnologies
Template Name: Smarthr - Bootstrap Admin Template
*/

(function () {
    "use strict";

	function qs(sel) { return document.querySelector(sel); }
	function qsa(sel) { return Array.from(document.querySelectorAll(sel)); }
	function on(el, evt, handler) { if (el) el.addEventListener(evt, handler); }

	//Top Online Contacts
	qsa('.chat-close').forEach(function (el) {
		on(el, 'click', function () {
			var chat = qs('.chat');
			if (chat) chat.classList.remove('show');
		});
	});

	//Top Online Contacts
	qsa('.chat-user-list').forEach(function (el) {
		on(el, 'click', function (e) {
			e.preventDefault();
			var chat = qs('.chat');
			if (chat) chat.classList.add('show');
		});
	});

	qsa('.close_profile').forEach(function (el) {
		on(el, 'click', function () {
			var rightSide = qs('.right-side-contact');
			if (rightSide) { rightSide.classList.add('hide-right-sidebar'); rightSide.classList.remove('show-right-sidebar'); }
			if (window.innerWidth > 991 && window.innerWidth < 1201) {
				var chat = qs('.chat');
				if (chat) chat.style.marginLeft = '0';
			}
			if (window.innerWidth < 992) {
				var chat2 = qs('.chat');
				if (chat2) chat2.classList.remove('hide-chatbar');
			}
		});
	});

	if (qs('.emoj-action')) {
		on(qs('.emoj-action'), 'click', function () {
			var el = qs('.emoj-group-list');
			if (el) el.style.display = (el.style.display === 'none' || !el.style.display) ? 'block' : 'none';
		});
	}

	if (qs('.emoj-action-foot')) {
		on(qs('.emoj-action-foot'), 'click', function () {
			var el = qs('.emoj-group-list-foot');
			if (el) el.style.display = (el.style.display === 'none' || !el.style.display) ? 'block' : 'none';
		});
	}

	//Chat Resize
	qsa('.close_profile').forEach(function (el) {
		on(el, 'click', function () {
			var rus = qs('.right-user-side');
			var comman = qs('.chat-center-blk .card-comman');
			if (rus) rus.classList.remove('open-message');
			if (comman) comman.classList.add('chat-center-space');
		});
	});
	on(qs('.profile-open'), 'click', function () {
		var rus = qs('.right-user-side');
		var comman = qs('.chat-center-blk .card-comman');
		if (rus) rus.classList.remove('add-setting');
		if (comman) comman.classList.remove('chat-center-space');
	});

	//Call Resize
	qsa('.close_profile').forEach(function (el) {
		on(el, 'click', function () {
			var rus = qs('.right-user-side');
			var videoInner = qs('.video-screen-inner');
			var rightParty = qs('.right-side-party');
			var meetingList = qs('.meeting-list');
			var chatRoom = document.getElementById('chat-room');
			var mainImg = qs('.main-img');
			var joinVideo = qs('.join-video');
			var callUserSide = qs('.call-user-side');
			if (rus) rus.classList.remove('open-message');
			if (videoInner) videoInner.classList.remove('video-space');
			if (rightParty) rightParty.classList.remove('open-message');
			if (meetingList) meetingList.classList.remove('add-meeting');
			if (chatRoom) chatRoom.classList.remove('open-chats');
			if (mainImg) mainImg.classList.remove('main-img-hide');
			if (joinVideo) joinVideo.classList.remove('main-img-hide');
			if (callUserSide) callUserSide.classList.add('add-setting');
		});
	});

	on(document.getElementById('show-message'), 'click', function () {
		var chatRoom = document.getElementById('chat-room');
		var rightParty = qs('.right-side-party');
		var mainImg = qs('.main-img');
		var joinVideo = qs('.join-video');
		if (chatRoom) chatRoom.classList.add('open-chats');
		if (rightParty) rightParty.classList.remove('open-message');
		if (mainImg) mainImg.classList.add('main-img-hide');
		if (joinVideo) joinVideo.classList.add('main-img-hide');
	});

	//Chat Search Visible
	on(qs('.chat-search-btn'), 'click', function () {
		var el = qs('.chat-search');
		if (el) el.classList.add('visible-chat');
	});
	on(qs('.close-btn-chat'), 'click', function () {
		var el = qs('.chat-search');
		if (el) el.classList.remove('visible-chat');
	});
	on(qs('.chat-search .form-control'), 'keyup', function () {
		var value = this.value.toLowerCase();
		qsa('.chat .chat-body .messages .chats').forEach(function (el) {
			el.style.display = el.textContent.toLowerCase().indexOf(value) > -1 ? '' : 'none';
		});
	});

	on(qs('.guest-off'), 'click', function () {
		this.classList.toggle('activate');
		var el = qs('.chat-active-users');
		if (el) el.classList.toggle('show-active-users');
	});
})();
