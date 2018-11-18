﻿import { Post } from '../../../utilities/requestHelper';

export default {
    props: ['closeDlg', 'showSnack'],
    data: () => ({
        username: '',
        usernameRules: [
            v => !!v || '请输入用户名',
            v => (v && v.length <= 30) || '用户名长度不能大于 30'
        ],
        email: '',
        emailRules: [
            v => !!v || '请输入邮箱地址',
            v => /.+@.+\..+/.test(v) || '邮箱地址格式不正确'
        ],
        sent: false,
        submitting: false,
        valid: false,
        token: '',
        tokenRules: [
            v => !!v || '请输入验证码'
        ],
        password: '',
        passwordRules: [
            v => !!v || '请输入新密码'
        ],
        confirmPassword: '',
        confirmPasswordRules: [
            v => !!v || '请输入确认密码'
        ]
    }),
    methods: {
        reset: function () {
            if (this.$refs.form.validate()) {
                this.submitting = true;
                Post('/Account/ResetPassword', { userName: this.username, token: this.token, password: this.password, confirmPassword: this.confirmPassword })
                    .then(res => res.json())
                    .then(data => {
                        if (data.isSucceeded) {
                            this.showSnack('密码重置成功', 'success', 3000);
                            this.closeDlg();
                        }
                        else this.showSnack(data.errorMessage, 'error', 3000);
                        this.submitting = false;
                    })
                    .catch(() => {
                        this.showSnack('密码重置失败', 'error', 3000);
                        this.submitting = false;
                    });
            }
        },
        sendEmail: function () {
            if (this.$refs.form.validate()) {
                this.password = '';
                this.token = '';
                this.confirmPassword = '';
                this.submitting = true;
                Post('/Account/SendPasswordResetToken', { userName: this.username, email: this.email })
                    .then(res => res.json())
                    .then(data => {
                        if (data.isSucceeded) this.sent = true;
                        else this.showSnack(data.errorMessage, 'error', 3000);
                        this.submitting = false;
                    })
                    .catch(() => {
                        this.showSnack('请求失败', 'error', 3000);
                        this.submitting = false;
                    });
            }
        },
        backward: function () {
            this.sent = false;
        },
        clearForm: function () {
            this.password = '';
            this.token = '';
            this.confirmPassword = '';
            this.username = '';
            this.email = '';
            this.valid = false;
            this.sent = false;
        }
    }
};